using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

internal readonly record struct OsdLayout(uint Version, int Stride, int Offset, int Count);
// Positioned：本次寫入是否實際帶有位置標籤。Stale：唯讀驗證時發現目前的定位狀態需要重寫（見 Access）。
internal readonly record struct OsdResult(string State, int Entry = -1, string Text = "", uint Frame = 0, uint Version = 0, bool Positioned = false, bool Stale = false);

// 遊戲內時鐘位置。Rtss＝不加任何標籤，沿用 RTSS 自身 OSD 位置（Avalonia-2 原行為）。
// 其餘使用 RTSS 官方 sticky 位置標籤 <Pn>（SDK RTSSSharedMemorySample 與 OverlayEditor 的 COverlayLayer::GetHypertext；
// n＝列×3＋欄：2 右上、6 左下、8 右下），共享記憶體 v2.11 起支援，本程式只接受 v2.14 以上。
// 不提供 sticky 左上（<P0>）：實測會與其他程式預設排在左上的文字重疊；要左上請用 Rtss（RTSS 預設即左上，且依序排列不重疊）。
public enum OsdPlacement { Rtss, TopRight, BottomLeft, BottomRight }

// 移植自 RTSS-1 原型 rtss-clock-probe/SharedOsd.cs（37／37 隔離測試、正式映射實測）。
// 協定、驗證規則、dwBusy 同步與兩階段尋槽保持不變；差異只有：
//   1. Owner 改為本產品專屬名稱，與原型不共用槽。
//   2. 文字長度由固定 8 字元放寬為 1–63 個可列印 ASCII 字元，以支援 HH:mm 與 12 小時制。
//   3. 新增唯讀的 Inspect，供狀態檢查與測試觀察，不寫入任何位元組。
//   4. Avalonia-2-POSITION：可選的 sticky 位置標籤與「排在最後的槽」配置規則（見 PlacementTag 與 Access）。
internal static class SharedOsd
{
    internal const string MapName = "RTSSSharedMemoryV2";
    internal const string Owner = "GameClockOverlayAvalonia";
    internal const uint Signature = 0x52545353;
    internal const int ExtendedText = 512, MaxText = 63;
    internal static int TextOffset(uint version) => version >= 0x20014 ? 266752 : ExtendedText;
    internal static int TextCapacity(uint version) => version >= 0x20014 ? 32768 : 4096;
    // 測試專用：在認領空槽、重新確認之前呼叫（參數為槽號），用來決定性地模擬其他程式在鎖外認領同一槽。產品程式從不設定。
    internal static Action<int>? TestBeforeClaim;
    internal static readonly byte[] OwnerBytes = Encoding.ASCII.GetBytes(Owner + '\0');

    // RTSSSharedMemory.h:309-400。DWORD/LONG 為 4 位元組；僅解析已知的起始欄位。只接受 v2.14–v2.21。
    internal static OsdLayout Validate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 40) throw new InvalidDataException("header-truncated");
        if (U32(bytes, 0) != Signature) throw new InvalidDataException("signature-invalid-or-dead");
        uint version = U32(bytes, 4);
        if (version < 0x2000e || version > 0x20015)
            throw new InvalidDataException($"unsupported-version-0x{version:X8}");
        int header = version >= 0x20013 ? 96 : version >= 0x20012 ? 80 :
            version >= 0x20010 ? 72 : version >= 0x2000f ? 64 : 40;
        uint stride = U32(bytes, 20), offset = U32(bytes, 24), count = U32(bytes, 28);
        uint minimum = version >= 0x20014 ? 299520u : 266752u;
        if (stride < minimum || stride > 1048576 || offset < header || count < 2 || count > 1024 ||
            (ulong)offset + (ulong)stride * count > (ulong)bytes.Length)
            throw new InvalidDataException("osd-array-out-of-bounds");
        uint appStride = U32(bytes, 8), appOffset = U32(bytes, 12), appCount = U32(bytes, 16);
        ulong appEnd = (ulong)appOffset + (ulong)appStride * appCount;
        if (appCount != 0 && (appStride == 0 || appOffset < header || appEnd > (ulong)bytes.Length ||
            ((ulong)appOffset < (ulong)offset + (ulong)stride * count && appEnd > offset)))
            throw new InvalidDataException("app-array-invalid-or-overlaps-osd");
        return new(version, (int)stride, (int)offset, (int)count);
    }

    internal static uint U32(ReadOnlySpan<byte> bytes, int offset) => MemoryMarshal.Read<uint>(bytes.Slice(offset, 4));
    internal static void Put(Span<byte> bytes, int offset, uint value) => MemoryMarshal.Write(bytes.Slice(offset, 4), in value);
    internal static bool IsOwner(ReadOnlySpan<byte> entry) => entry.Slice(256, OwnerBytes.Length).SequenceEqual(OwnerBytes);
    internal static bool IsValidText(string text) => text.Length is > 0 and <= MaxText && text.All(c => c is >= ' ' and <= '~');

    // RTSS 依槽號順序排版，位置標籤會延續到後面的槽（ReadMe：placeholder layer 可決定「下一個 hypertext client」的輸出位置；
    // 實測未加標籤的後方槽會跟著移到右上）。因此只有在自己的槽之後沒有任何其他非空槽時才加標籤，否則退回不加標籤。
    internal static string PlacementTag(OsdPlacement placement) => placement switch
    {
        OsdPlacement.TopRight => "<P2><L0>",
        OsdPlacement.BottomLeft => "<P6><L0>",
        OsdPlacement.BottomRight => "<P8><L0>",
        _ => "",
    };
    internal static bool IsPlacementTag(ReadOnlySpan<byte> text) =>
        text.Length >= 8 && text[0] == '<' && text[1] == 'P' && text[2] is (byte)'2' or (byte)'6' or (byte)'8' && text[3..8].SequenceEqual("><L0>"u8);

    // 回傳：自己的槽（無則 -1）、自己以外最後一個非空槽（無則 0）、最高的空槽與第一個空槽（無則 -1）。第 0 槽永不配置。
    private static (int Own, int LastForeign, int LastFree, int FirstFree) Scan(ReadOnlySpan<byte> bytes, OsdLayout layout)
    {
        int own = -1, lastForeign = 0, lastFree = -1, firstFree = -1;
        for (int i = 1; i < layout.Count; i++)
        {
            var entry = bytes.Slice(layout.Offset + i * layout.Stride, layout.Stride);
            if (IsOwner(entry)) { if (own < 0) own = i; }
            else if (entry[256] != 0) lastForeign = i;
            else { lastFree = i; if (firstFree < 0) firstFree = i; }
        }
        return (own, lastForeign, lastFree, firstFree);
    }

    // 指標只存活於單次同步操作；SafeHandle 的指標參照在 finally 釋放。
    // 每次重新開啟映射，避免 RTSS 重啟後持續保留舊的共享記憶體物件。
    // placement 只影響寫入：Rtss 時尋槽與寫入內容與 Avalonia-2 完全相同。
    // 其他位置：盡量待在最高的槽。後面還有空槽就搬過去（同一次持鎖內清除舊槽），讓之後才啟動、
    // 依慣例取第一個空槽的程式排在本程式前面；沒有空槽可搬但自己已在所有非空槽之後就沿用。
    // 都做不到時沿用原本的槽或第一個空槽，並且不加標籤（Positioned=false），不讓位置延續到其他程式的文字。
    internal static unsafe OsdResult Access(string mapName, string? text = null, bool clear = false, OsdPlacement placement = OsdPlacement.Rtss)
    {
        if (text is not null && !IsValidText(text))
            throw new ArgumentException("Clock text must contain 1-63 printable ASCII characters.", nameof(text));
        try
        {
            using var map = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            if (view.Capacity > int.MaxValue) return new("mapping-too-large");
            byte* pointer = null;
            try
            {
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                if (pointer is null) return new("mapping-null");
                byte* start = pointer + view.PointerOffset;
                var bytes = new Span<byte>(start, checked((int)view.Capacity));
                OsdLayout layout = Validate(bytes);
                if (text is null && !clear)
                {
                    // 唯讀判斷是否需要提早重寫（所有位置都適用）：
                    // GCO-01：找不到自己的槽（RTSS 在兩次探測之間快速重啟、或槽被清除）→ 立即重寫，不沿用上一次的成功狀態。
                    // 定位時另外判斷：已定位但後方出現其他槽（需搬移或退回），或未定位但現在可以定位。
                    var (own, lastForeign, lastFree, _) = Scan(bytes, layout);
                    if (own < 0) return new("connected", Version: layout.Version, Stale: true);
                    if (placement == OsdPlacement.Rtss) return new("connected", Version: layout.Version);
                    bool positioned = IsPlacementTag(bytes.Slice(layout.Offset + own * layout.Stride + TextOffset(layout.Version), 8));
                    bool stale = positioned ? own < lastForeign || lastFree > own : own > lastForeign || lastFree > lastForeign;
                    return new("connected", Version: layout.Version, Positioned: positioned, Stale: stale);
                }
                // SDK 的 bit 0 鎖。保留其他位元，不會清除別人的鎖；忙碌時交由下次更新。
                ref int busy = ref *(int*)(start + 36);
                int observed = Volatile.Read(ref busy);
                if ((observed & 1) != 0 || Interlocked.CompareExchange(ref busy, observed | 1, observed) != observed)
                    return new("busy", Version: layout.Version);
                try
                {
                    if (Validate(bytes) != layout) return new("layout-changed");
                    int chosen = -1, previous = -1;
                    string tag = "";
                    if (!clear && placement != OsdPlacement.Rtss)
                    {
                        var (own, lastForeign, lastFree, firstFree) = Scan(bytes, layout);
                        if (lastFree > Math.Max(own, lastForeign))
                        {
                            // 舊槽要等確認新槽認領成功後才清除（見下方 GCO-02）。
                            previous = own; chosen = lastFree; tag = PlacementTag(placement);
                        }
                        else if (own > lastForeign) { chosen = own; tag = PlacementTag(placement); }
                        else chosen = own > 0 ? own : firstFree;
                        if (chosen < 0) return new("no-free-entry", Version: layout.Version);
                    }
                    else
                    {
                        // C# 15 labeled break：先尋找自己的槽，再尋找空槽；第 0 槽永不配置。清除只找自己的槽。
                        search: for (int pass = 0; pass < (clear ? 1 : 2); pass++)
                        {
                            for (int i = 1; i < layout.Count; i++)
                            {
                                var entry = bytes.Slice(layout.Offset + i * layout.Stride, layout.Stride);
                                if (pass == 0 ? IsOwner(entry) : entry[256] == 0)
                                {
                                    chosen = i;
                                    break search;
                                }
                            }
                        }
                    }
                    if (chosen < 0) return new(clear ? "already-clear" : "no-free-entry", Version: layout.Version);
                    var selected = bytes.Slice(layout.Offset + chosen * layout.Stride, layout.Stride);
                    if (clear) selected.Clear();
                    else
                    {
                        if (!IsOwner(selected))
                        {
                            // GCO-02：SDK 範例在 dwBusy 鎖之外檢查空槽並寫入 Owner，所以掃描之後目標仍可能被其他程式認領。
                            // 認領前重新確認目標仍是空槽；已有 Owner 就放棄，絕不以「不是自己」為由清除。
                            // 這只能縮小、不能消除時間窗：不遵守同一把鎖的寫入者仍可能在確認之後才寫入（見 AVALONIA-2-POSITION.md）。
                            TestBeforeClaim?.Invoke(chosen);
                            if (selected[256] != 0) return new("claim-conflict", Version: layout.Version);
                            if (previous > 0) bytes.Slice(layout.Offset + previous * layout.Stride, layout.Stride).Clear();
                            selected.Clear();
                            OwnerBytes.CopyTo(selected.Slice(256));
                        }
                        int offset = TextOffset(layout.Version), capacity = TextCapacity(layout.Version);
                        selected.Slice(offset, capacity).Clear();
                        Encoding.ASCII.GetBytes(tag + text!, selected.Slice(offset, capacity - 1));
                        // 寫入後再確認 Owner 仍是自己；已被他人覆寫就不再動這個槽，交由下次重試。
                        if (!IsOwner(selected)) return new("claim-conflict", Version: layout.Version);
                    }
                    uint frame = unchecked((uint)Interlocked.Increment(ref *(int*)(start + 32)));
                    // 讀回只取時間文字本身（略過位置標籤），供狀態顯示與比對。
                    string readback = clear ? "" : Encoding.ASCII.GetString(selected.Slice(TextOffset(layout.Version) + tag.Length, text!.Length));
                    return new(clear ? "cleared" : "updated", chosen, readback, frame, layout.Version, Positioned: tag.Length > 0);
                }
                finally { Interlocked.And(ref busy, ~1); }
            }
            finally { if (pointer is not null) view.SafeMemoryMappedViewHandle.ReleasePointer(); }
        }
        catch (FileNotFoundException) { return new("not-running"); }
        catch (InvalidDataException e) { return new(e.Message); }
        catch (UnauthorizedAccessException) { return new("access-denied"); }
        catch (IOException e) { return new($"mapping-error-0x{e.HResult:X8}"); }
    }

    // 唯讀檢查：只以讀取權限開啟，回傳本 Owner 的槽數與第一個槽的文字；不取鎖、不遞增計數器。
    internal static (string State, int Owned, int FirstEntry, string Text, string Tag) Inspect(string mapName)
    {
        try
        {
            using var map = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
            using var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (view.Capacity > int.MaxValue) return ("mapping-too-large", 0, -1, "", "");
            var bytes = new byte[view.Capacity];
            view.ReadArray(0, bytes, 0, bytes.Length);
            OsdLayout layout = Validate(bytes);
            int owned = 0, first = -1; string text = "", tag = "";
            for (int i = 1; i < layout.Count; i++)
            {
                var entry = bytes.AsSpan(layout.Offset + i * layout.Stride, layout.Stride);
                if (!IsOwner(entry)) continue;
                owned++;
                if (first >= 0) continue;
                first = i;
                // 最多 8 字元的位置標籤＋63 字元時間文字＋NUL；Text 不含標籤，標籤另外放在 Tag。
                var slot = entry.Slice(TextOffset(layout.Version), MaxText + 9);
                int end = slot.IndexOf((byte)0);
                var raw = end < 0 ? slot : slot[..end];
                if (IsPlacementTag(raw)) { tag = Encoding.ASCII.GetString(raw[..8]); raw = raw[8..]; }
                text = Encoding.ASCII.GetString(raw);
            }
            return ("connected", owned, first, text, tag);
        }
        catch (FileNotFoundException) { return ("not-running", 0, -1, "", ""); }
        catch (InvalidDataException e) { return (e.Message, 0, -1, "", ""); }
        catch (UnauthorizedAccessException) { return ("access-denied", 0, -1, "", ""); }
        catch (IOException e) { return ($"mapping-error-0x{e.HResult:X8}", 0, -1, "", ""); }
    }
}
