#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <thumbcache.h>
#include <shlwapi.h>
#include <cstdio>
#include <string>
#include <stdexcept>

static void Check(HRESULT hr, const char* stage) { if (FAILED(hr)) { std::fprintf(stderr,"FAIL %s HRESULT=0x%08lx\n",stage,(unsigned long)hr); throw std::runtime_error(stage); } }
template<class T> struct Com { T* p=nullptr; ~Com(){if(p)p->Release();} T** out(){return &p;} T* operator->(){return p;} };
typedef HRESULT (STDAPICALLTYPE *GetClassObject)(REFCLSID,REFIID,LPVOID*);
static GetClassObject factory;
static bool registeredMode=false;
static const GUID ThumbnailIid={0xe357fccd,0xa995,0x4576,{0xb0,0x1f,0x23,0x46,0x30,0x15,0x4e,0x96}};
static void Create(const wchar_t* id, REFIID iid, void** value) { CLSID clsid; Check(CLSIDFromString(id,&clsid),"CLSID"); if(registeredMode){Check(CoCreateInstance(clsid,nullptr,CLSCTX_INPROC_SERVER,iid,value),"CoCreateInstance");return;} Com<IClassFactory> f; Check(factory(clsid,IID_IClassFactory,(void**)f.out()),"DllGetClassObject"); Check(f->CreateInstance(nullptr,iid,value),"CreateInstance"); }
static void Icon(const wchar_t* path) {
 Com<IExtractIconW> icon; Create(L"{1F869CEE-4FDA-35D9-896F-43975A87D1F6}",IID_IExtractIconW,(void**)icon.out());
 Com<IPersistFile> file;Check(icon->QueryInterface(IID_IPersistFile,(void**)file.out()),"icon IPersistFile");Check(file->Load(path,STGM_READ),"icon Load");
 wchar_t location[260]={};int index=0;UINT flags=0;Check(icon->GetIconLocation(0,location,260,&index,&flags),"GetIconLocation");
 HICON large=nullptr,small=nullptr;Check(icon->Extract(path,index,&large,&small,MAKELONG(64,16)),"Extract");
 bool valid=large&&small;if(large)DestroyIcon(large);if(small)DestroyIcon(small);if(!valid)throw std::runtime_error("missing icon handles");
 std::puts("PASS native icon callbacks");
}
static void Info(const wchar_t* path) {
 Com<IQueryInfo> info;Create(L"{946435a5-fe96-416d-99db-e94ee9fb46c8}",IID_IQueryInfo,(void**)info.out());Com<IPersistFile> file;
 Check(info->QueryInterface(IID_IPersistFile,(void**)file.out()),"info IPersistFile");Check(file->Load(path,STGM_READ),"info Load");
 LPWSTR text=nullptr;Check(info->GetInfoTip(0,&text),"GetInfoTip");bool valid=text&&*text; if(text){std::wprintf(L"INFOTIP %ls\n",text);CoTaskMemFree(text);}if(!valid)throw std::runtime_error("empty infotip");std::puts("PASS native info callbacks");
}
static void Thumbnail(const wchar_t* path) {
 Com<IInitializeWithStream> init;Create(L"{d747c5a7-2f66-4b7d-8301-8531838e4ed3}",IID_IInitializeWithStream,(void**)init.out());Com<IThumbnailProvider> thumb;
 Check(init->QueryInterface(ThumbnailIid,(void**)thumb.out()),"thumbnail QI");Com<IStream> input;Check(SHCreateStreamOnFileEx(path,STGM_READ|STGM_SHARE_DENY_NONE,FILE_ATTRIBUTE_NORMAL,FALSE,nullptr,input.out()),"thumbnail input");
 Check(init->Initialize(input.p,STGM_READ),"thumbnail Initialize");HBITMAP bitmap=nullptr;WTS_ALPHATYPE alpha=WTSAT_UNKNOWN;HRESULT hr=thumb->GetThumbnail(64,&bitmap,&alpha);if(bitmap)DeleteObject(bitmap);Check(hr,"GetThumbnail (EnableThumbnail setting must be True)");if(!bitmap)throw std::runtime_error("missing thumbnail bitmap");std::printf("PASS native thumbnail alpha=%d\n",(int)alpha);
}
static void Context() {
 Com<IShellExtInit> init;Create(L"{dcb629fc-f86f-456f-8e24-98b9b2643a9b}",IID_IShellExtInit,(void**)init.out());Com<IContextMenu> menu;Check(init->QueryInterface(IID_IContextMenu,(void**)menu.out()),"context IContextMenu");std::puts("PASS native context interfaces");
}
int wmain(int argc,wchar_t**argv) {
 if(argc!=3 && argc!=4){std::fputs("Usage: NativeComSmoke host.comhost.dll fixture.apk [--registered]\n",stderr);return 2;}
 registeredMode=argc==4 && std::wstring(argv[3])==L"--registered";
 HRESULT initialized=CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);if(FAILED(initialized))return 2;
 wchar_t temp[MAX_PATH],file[MAX_PATH];GetTempPathW(MAX_PATH,temp);GetTempFileNameW(temp,L"acs",0,file);std::wstring empty=std::wstring(file)+L".apk";MoveFileW(file,empty.c_str());
 try {
 if(!registeredMode) { HMODULE module=LoadLibraryExW(argv[1],nullptr,LOAD_WITH_ALTERED_SEARCH_PATH);if(!module){std::fprintf(stderr,"LoadLibrary error=%lu\n",GetLastError());throw std::runtime_error("comhost load");}factory=(GetClassObject)GetProcAddress(module,"DllGetClassObject");if(!factory)throw std::runtime_error("missing export"); }
 Icon(empty.c_str());HANDLE writer=CreateFileW(empty.c_str(),GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,0,nullptr);if(writer==INVALID_HANDLE_VALUE)throw std::runtime_error("empty callback locked writer");DWORD written;char b='P';BOOL ok=WriteFile(writer,&b,1,&written,nullptr);CloseHandle(writer);if(!ok||written!=1)throw std::runtime_error("download write failed");std::wstring renamed=empty+L".renamed";if(!MoveFileW(empty.c_str(),renamed.c_str()))throw std::runtime_error("download rename blocked");DeleteFileW(renamed.c_str());std::puts("PASS native empty download write/rename");
 Icon(argv[2]);Info(argv[2]);Thumbnail(argv[2]);Context();
 HMODULE clr=GetModuleHandleW(L"coreclr.dll");wchar_t runtime[32768]={};if(!clr||!GetModuleFileNameW(clr,runtime,32768))throw std::runtime_error("coreclr not loaded");std::wprintf(L"CORECLR %ls\n",runtime);
 if(std::wstring(runtime).find(L"\\10.")==std::wstring::npos)throw std::runtime_error("unexpected runtime version path");
 wchar_t actualHost[32768]={};HMODULE hostModule=GetModuleHandleW(L"ApkShellext2.comhost.dll");if(!hostModule||!GetModuleFileNameW(hostModule,actualHost,32768))throw std::runtime_error("comhost module evidence missing");std::wprintf(L"COMHOST %ls\n",actualHost);if(_wcsicmp(actualHost,argv[1])!=0)throw std::runtime_error("Loaded comhost differs from expected candidate");std::puts(registeredMode?"PASS native registered COM host activation":"PASS native unregistered COM host activation");CoUninitialize();return 0;
 }catch(const std::exception&e){std::fprintf(stderr,"FAIL %s\n",e.what());DeleteFileW(empty.c_str());CoUninitialize();return 1;}
}
