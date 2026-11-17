using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GameClockOverlay.AvaloniaApp.Services.Rtss;

// 啟動前驗證 RTSS.exe 的 Authenticode 簽章與發行者，失敗時以錯誤碼回報。
// 原因：RTSS 要求系統管理員權限，每次自動啟動都會出現 UAC 提示；若安裝資料夾可被一般使用者寫入
//（非系統磁碟根目錄的預設權限即是如此），或路徑來自使用者可寫的設定檔／HKCU，被替換的 RTSS.exe 會借用這個
// 使用者已習慣按「是」的提示取得系統管理員權限。只接受由 RTSS 發行者有效簽署的檔案。
public sealed class RtssTrustException(string code) : Exception(code) { public string Code { get; } = code; }

internal static class RtssTrust
{
    // RTSS 7.x 由 MSI 以 EV 憑證簽署（本機 7.3.5：CN=MICRO-STAR INTERNATIONAL CO., LTD.，GlobalSign EV CA）。
    internal const string Publisher="MICRO-STAR INTERNATIONAL CO., LTD.";

    // 以已開啟的檔案控制代碼驗證，讓驗證的內容與之後啟動的檔案是同一個（呼叫端持有 FileShare.Read，期間無法替換）。
    // 簽署者憑證取自同一次 WinVerifyTrust 的驗證狀態，不另外讀檔。不檢查撤銷、只用快取：本程式承諾不發出網路請求。
    internal static string? Check(string path,SafeHandle file)
    {
        var (result,signer)=Verify(path,file);
        if(result!=0)return result is TrustENoSignature or TrustESubjectFormUnknown?"unsigned":"signature-invalid";
        return string.Equals(signer,Publisher,StringComparison.OrdinalIgnoreCase)?null:"publisher-mismatch";
    }

    private const int TrustENoSignature=unchecked((int)0x800B0100),TrustESubjectFormUnknown=unchecked((int)0x800B0003);
    private const uint StateVerify=1,StateClose=2;
    private static readonly Guid GenericVerifyV2=new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInfo { public uint Size; public nint Path; public nint File; public nint KnownSubject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size; public nint PolicyCallbackData,SipClientData; public uint UiChoice,RevocationChecks,UnionChoice;
        public nint FileInfo; public uint StateAction; public nint StateData,UrlReference; public uint ProvFlags,UiContext; public nint SignatureSettings;
    }
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern int WinVerifyTrust(nint hwnd,in Guid action,ref TrustData data);
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern nint WTHelperProvDataFromStateData(nint state);
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern nint WTHelperGetProvSignerFromChain(nint provider,uint signer,[MarshalAs(UnmanagedType.Bool)] bool counterSigner,uint counterIndex);
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern nint WTHelperGetProvCertFromChain(nint signer,uint index);

    private static unsafe (int Result,string? Signer) Verify(string path,SafeHandle file)
    {
        bool added=false;
        nint text=Marshal.StringToHGlobalUni(path);
        var info=new FileInfo{Size=(uint)sizeof(FileInfo),Path=text};
        var data=new TrustData
        {
            Size=(uint)sizeof(TrustData),UiChoice=2/*WTD_UI_NONE*/,RevocationChecks=0/*WTD_REVOKE_NONE*/,UnionChoice=1/*WTD_CHOICE_FILE*/,
            FileInfo=(nint)(&info),StateAction=StateVerify,
            ProvFlags=0x10/*WTD_REVOCATION_CHECK_NONE*/|0x1000/*WTD_CACHE_ONLY_URL_RETRIEVAL*/
        };
        try
        {
            file.DangerousAddRef(ref added);
            info.File=file.DangerousGetHandle();
            // INVALID_HANDLE_VALUE：不顯示任何介面。
            int result=WinVerifyTrust(-1,GenericVerifyV2,ref data);
            return (result,result==0?SignerName(data.StateData):null);
        }
        finally
        {
            // 驗證狀態一律以 WTD_STATEACTION_CLOSE 釋放。
            data.StateAction=StateClose;WinVerifyTrust(-1,GenericVerifyV2,ref data);
            if(added)file.DangerousRelease();
            Marshal.FreeHGlobal(text);
        }
    }

    // CRYPT_PROVIDER_CERT（x64）：DWORD cbStruct 之後補齊到 8，接著是 PCCERT_CONTEXT pCert。第 0 張為簽署者憑證。
    private static string? SignerName(nint state)
    {
        nint provider=WTHelperProvDataFromStateData(state);if(provider==0)return null;
        nint signer=WTHelperGetProvSignerFromChain(provider,0,false,0);if(signer==0)return null;
        nint cert=WTHelperGetProvCertFromChain(signer,0);if(cert==0)return null;
        nint context=Marshal.ReadIntPtr(cert,IntPtr.Size);if(context==0)return null;
        try
        {
            // 以控制代碼建立會複製憑證內容，不受驗證狀態釋放影響。
            using var certificate=new X509Certificate2(context);
            return certificate.GetNameInfo(X509NameType.SimpleName,false);
        }
        catch(CryptographicException){return null;}
    }
}
