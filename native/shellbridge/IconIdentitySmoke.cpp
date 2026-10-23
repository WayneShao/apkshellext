#define UNICODE
#include <windows.h>
#include <shlobj.h>
#include <cstdio>
#include <string>
#include <stdexcept>
template<class T>struct Com{T*p=nullptr;~Com(){if(p)p->Release();}T**out(){return &p;}T*operator->(){return p;}};
using GetClass=HRESULT(WINAPI*)(REFCLSID,REFIID,void**);
static GetClass factory;
static int failures=0;
static void check(bool ok,const char*what){printf("%s %s\n",ok?"PASS":"FAIL",what);if(!ok)++failures;}
static void hr(HRESULT h){if(FAILED(h))throw std::runtime_error("COM failure");}
struct Identity{std::wstring key;int index;UINT flags;HRESULT result;};
static Identity identity(const std::wstring&path,bool ansi=false,UINT capacity=260,UINT input=0){
 CLSID cls;hr(CLSIDFromString(L"{1F869CEE-4FDA-35D9-896F-43975A87D1F6}",&cls));Com<IClassFactory>f;hr(factory(cls,IID_IClassFactory,(void**)f.out()));Com<IPersistFile>file;hr(f->CreateInstance(nullptr,IID_IPersistFile,(void**)file.out()));hr(file->Load(path.c_str(),0));Identity id{};
 if(ansi){Com<IExtractIconA>icon;hr(file->QueryInterface(IID_IExtractIconA,(void**)icon.out()));char key[1024]={};id.result=icon->GetIconLocation(input,key,capacity,&id.index,&id.flags);for(const char*p=key;*p;++p)id.key+=(unsigned char)*p;}
 else{Com<IExtractIconW>icon;hr(file->QueryInterface(IID_IExtractIconW,(void**)icon.out()));wchar_t key[1024]={};id.result=icon->GetIconLocation(input,key,capacity,&id.index,&id.flags);id.key=key;}
 return id;
}
static void write(const std::wstring&p,DWORD size,bool future=false){HANDLE f=CreateFileW(p.c_str(),GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);if(f==INVALID_HANDLE_VALUE)throw std::runtime_error("fixture create");char b[128]={};DWORD n;WriteFile(f,b,size,&n,nullptr);if(future){FILETIME t;GetSystemTimeAsFileTime(&t);ULARGE_INTEGER stamp;stamp.LowPart=t.dwLowDateTime;stamp.HighPart=t.dwHighDateTime;stamp.QuadPart+=100000000; t.dwLowDateTime=stamp.LowPart;t.dwHighDateTime=stamp.HighPart;SetFileTime(f,nullptr,nullptr,&t);}CloseHandle(f);}
int wmain(int argc,wchar_t**argv){if(argc!=3)return 2;CoInitializeEx(nullptr,COINIT_APARTMENTTHREADED);try{HMODULE dll=LoadLibraryW(argv[1]);if(!dll)throw std::runtime_error("load DLL");factory=(GetClass)GetProcAddress(dll,"DllGetClassObject");if(!factory)throw std::runtime_error("export");std::wstring root=argv[2];CreateDirectoryW(root.c_str(),nullptr);CreateDirectoryW((root+L"\\one").c_str(),nullptr);CreateDirectoryW((root+L"\\two").c_str(),nullptr);
 std::wstring a=root+L"\\one\\same.apk",b=root+L"\\two\\same.apk",u=root+L"\\应用🐈.apk",v=root+L"\\应用🐕.apk";write(a,32);write(b,32);write(u,32);write(v,32);auto first=identity(a),other=identity(b);
 check(!first.key.empty(),"nonempty icon identity");check(first.key!=other.key||first.index!=other.index,"same filename in different folders gets distinct identity");check(!(first.flags&GIL_DONTCACHE),"normal completed file remains cacheable");check(first.key==identity(a).key,"unchanged metadata has stable identity");
 write(a,32,true);check(first.key!=identity(a).key,"same-length rewritten file changes identity");auto rewritten=identity(a);write(a,64,true);check(rewritten.key!=identity(a).key,"growing partial file changes identity");write(a,0);check(identity(a).flags&GIL_DONTCACHE,"empty download remains uncached");write(a,8);check(identity(a).flags&GIL_DONTCACHE,"very incomplete download remains uncached");
 auto unicode=identity(u),ansi=identity(u,true);check(!unicode.key.empty()&&unicode.key==ansi.key,"ANSI and Unicode interfaces share lossless identity");check(ansi.key!=identity(v,true).key,"Unicode supplementary names remain distinct through ANSI interface");check(identity(u,false,4).flags&GIL_DONTCACHE,"short Unicode buffer disables caching");check(identity(u,true,4).flags&GIL_DONTCACHE,"short ANSI buffer disables caching");check(identity(root+L"\\missing.apk").flags&GIL_DONTCACHE,"missing file disables caching");check(identity(u,false,260,GIL_DEFAULTICON).result==S_FALSE,"default icon request delegates to Shell");check(identity(u,false,260,GIL_ASYNC).result!=E_PENDING,"async flag does not claim free-threaded object");
 DeleteFileW(a.c_str());DeleteFileW(b.c_str());DeleteFileW(u.c_str());DeleteFileW(v.c_str());RemoveDirectoryW((root+L"\\one").c_str());RemoveDirectoryW((root+L"\\two").c_str());RemoveDirectoryW(root.c_str());FreeLibrary(dll);CoUninitialize();return failures?1:0;
 }catch(const std::exception&e){fprintf(stderr,"FAIL %s error=%lu\n",e.what(),GetLastError());CoUninitialize();return 2;}}
