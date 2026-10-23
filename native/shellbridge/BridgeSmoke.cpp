#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <thumbcache.h>
#include <shlwapi.h>
#include <cstdio>
#include <string>
#include <stdexcept>
template<class T>struct Com{T*p=nullptr;~Com(){if(p)p->Release();}T**out(){return &p;}T*operator->(){return p;}};
using GetClass=HRESULT(WINAPI*)(REFCLSID,REFIID,void**);
static GetClass getClass;
static void check(bool ok,const char*what){if(!ok)throw std::runtime_error(what);}
static void hr(HRESULT h,const char*what){if(FAILED(h)){printf("HRESULT %08lx\n",h);throw std::runtime_error(what);}}
static void create(const wchar_t*id,REFIID iid,void**p){CLSID c;hr(CLSIDFromString(id,&c),"CLSID");Com<IClassFactory>f;hr(getClass(c,IID_IClassFactory,(void**)f.out()),"factory");hr(f->CreateInstance(nullptr,iid,p),"create");}
static HRESULT extract(const wchar_t*path,UINT size=64){Com<IExtractIconW>icon;create(L"{1F869CEE-4FDA-35D9-896F-43975A87D1F6}",IID_IExtractIconW,(void**)icon.out());Com<IPersistFile>file;hr(icon->QueryInterface(IID_IPersistFile,(void**)file.out()),"persist");hr(file->Load(path,0),"load");HICON large=nullptr,small=nullptr;HRESULT result=icon->Extract(path,0,&large,&small,MAKELONG(size,16));if(SUCCEEDED(result))check(large&&small,"icon handles");if(large)DestroyIcon(large);if(small)DestroyIcon(small);return result;}
int wmain(int argc,wchar_t**argv){if(argc!=3)return 2;CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);try{HMODULE mod=LoadLibraryW(argv[1]);if(!mod)printf("LoadLibrary error %lu\n",GetLastError());check(mod!=nullptr,"load DLL");getClass=(GetClass)GetProcAddress(mod,"DllGetClassObject");check(getClass!=nullptr,"export");auto unload=(HRESULT(WINAPI*)())GetProcAddress(mod,"DllCanUnloadNow");check(unload&&unload()==S_OK,"initial unload");
 hr(extract(argv[2],1024),"large pipe and icons");puts("PASS bounded 4 MB image pipe, dual icons");
 {Com<IQueryInfo>info;create(L"{946435a5-fe96-416d-99db-e94ee9fb46c8}",IID_IQueryInfo,(void**)info.out());Com<IPersistFile>file;hr(info->QueryInterface(IID_IPersistFile,(void**)file.out()),"info persist");hr(file->Load(argv[2],0),"info load");LPWSTR text=nullptr;hr(info->GetInfoTip(0,&text),"info");check(text&&std::wstring(text)==L"APK protocol fixture","info contents");CoTaskMemFree(text);}
 puts("PASS UTF16 info protocol");
 {Com<IInitializeWithStream>init;create(L"{d747c5a7-2f66-4b7d-8301-8531838e4ed3}",__uuidof(IInitializeWithStream),(void**)init.out());Com<IThumbnailProvider>thumb;hr(init->QueryInterface(__uuidof(IThumbnailProvider),(void**)thumb.out()),"thumb QI");BYTE data[]={1,2,3,4,5};Com<IStream>stream;stream.p=SHCreateMemStream(data,5);check(stream.p,"mem stream");LARGE_INTEGER seek{};seek.QuadPart=2;hr(stream->Seek(seek,STREAM_SEEK_SET,nullptr),"seek");hr(init->Initialize(stream.p,0),"stream init");HBITMAP bitmap=nullptr;WTS_ALPHATYPE alpha;hr(thumb->GetThumbnail(64,&bitmap,&alpha),"thumbnail snapshot");check(bitmap&&alpha==WTSAT_ARGB,"thumbnail results");DeleteObject(bitmap);ULARGE_INTEGER pos;seek.QuadPart=0;hr(stream->Seek(seek,STREAM_SEEK_CUR,&pos),"position");check(pos.QuadPart==2,"position restored");}
 puts("PASS memory IStream snapshot and cursor restoration");
 {Com<IShellExtInit>init;create(L"{dcb629fc-f86f-456f-8e24-98b9b2643a9b}",IID_IShellExtInit,(void**)init.out());Com<IContextMenu>menu;hr(init->QueryInterface(IID_IContextMenu,(void**)menu.out()),"menu QI");}
 check(FAILED(extract(L"truncated.apk")),"truncated response rejected");check(FAILED(extract(L"oversize.apk")),"oversized response rejected");puts("PASS invalid response rejection");
 ULONGLONG start=GetTickCount64();check(extract(L"timeout.apk")==HRESULT_FROM_WIN32(ERROR_TIMEOUT),"timeout HRESULT");check(GetTickCount64()-start<10000,"timeout wall bound");puts("PASS 8 second timeout");
 check(!GetModuleHandleW(L"coreclr.dll"),"CoreCLR must not load");check(unload()==S_OK,"all objects released");puts("PASS native-only host and COM unloading");FreeLibrary(mod);CoUninitialize();return 0;
 }catch(const std::exception&e){fprintf(stderr,"FAIL %s\n",e.what());CoUninitialize();return 1;}}
