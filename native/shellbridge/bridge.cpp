#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shlobj.h>
#include <thumbcache.h>
#include <shellapi.h>
#include <string>
#include <vector>
#include <algorithm>
#include <cstdint>
#include <new>
#include <memory>

static HMODULE module;
static volatile LONG objects=0, locks=0;
static const CLSID ids[]={
 {0x1f869cee,0x4fda,0x35d9,{0x89,0x6f,0x43,0x97,0x5a,0x87,0xd1,0xf6}},
 {0x946435a5,0xfe96,0x416d,{0x99,0xdb,0xe9,0x4e,0xe9,0xfb,0x46,0xc8}},
 {0xd747c5a7,0x2f66,0x4b7d,{0x83,0x01,0x85,0x31,0x83,0x8e,0x4e,0xd3}},
 {0xdcb629fc,0xf86f,0x456f,{0x8e,0x24,0x98,0xb9,0xb2,0x64,0x3a,0x9b}}};
struct Handle { HANDLE h=nullptr; Handle()=default; explicit Handle(HANDLE p):h(p){} ~Handle(){if(h&&h!=INVALID_HANDLE_VALUE)CloseHandle(h);} Handle(const Handle&)=delete; Handle& operator=(const Handle&)=delete; };
static HRESULT error(){DWORD e=GetLastError();return HRESULT_FROM_WIN32(e?e:ERROR_GEN_FAILURE);}
static std::wstring quote(const std::wstring&s){std::wstring o=L"\"";size_t n=0;for(wchar_t c:s){if(c==L'\\'){++n;continue;}o.append(c==L'"'?2*n+1:n,L'\\');o+=c;n=0;}o.append(2*n,L'\\');return o+L'"';}
static std::wstring worker(){wchar_t p[32768];DWORD n=GetModuleFileNameW(module,p,32768);if(!n||n>=32768)throw HRESULT(E_FAIL);std::wstring s(p,n);return s.substr(0,s.find_last_of(L"\\/")+1)+L"ApkShellext2.Worker.exe";}
static std::wstring command(const std::vector<std::wstring>&args){std::wstring c=quote(worker());for(auto&s:args)c+=L" "+quote(s);if(c.size()>=32767)throw HRESULT(E_INVALIDARG);return c;}
struct Attributes { LPPROC_THREAD_ATTRIBUTE_LIST p=nullptr; ~Attributes(){if(p){DeleteProcThreadAttributeList(p);HeapFree(GetProcessHeap(),0,p);}} };
static HRESULT run(const std::vector<std::wstring>&args,std::vector<BYTE>&out){
 std::wstring exe=worker(),cmd=command(args);SECURITY_ATTRIBUTES sa{sizeof(sa),nullptr,TRUE};Handle rd,wr;
 if(!CreatePipe(&rd.h,&wr.h,&sa,0)||!SetHandleInformation(rd.h,HANDLE_FLAG_INHERIT,0))return error();
 Handle nul(CreateFileW(L"NUL",GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE,&sa,OPEN_EXISTING,0,nullptr));if(nul.h==INVALID_HANDLE_VALUE)return error();
 Handle job(CreateJobObjectW(nullptr,nullptr));if(!job.h)return error();JOBOBJECT_EXTENDED_LIMIT_INFORMATION ji{};ji.BasicLimitInformation.LimitFlags=JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
 if(!SetInformationJobObject(job.h,JobObjectExtendedLimitInformation,&ji,sizeof(ji)))return error();
 SIZE_T bytes=0;InitializeProcThreadAttributeList(nullptr,1,0,&bytes);Attributes attrs;attrs.p=(LPPROC_THREAD_ATTRIBUTE_LIST)HeapAlloc(GetProcessHeap(),0,bytes);if(!attrs.p)return E_OUTOFMEMORY;
 if(!InitializeProcThreadAttributeList(attrs.p,1,0,&bytes)){HeapFree(GetProcessHeap(),0,attrs.p);attrs.p=nullptr;return error();}
 HANDLE inherited[]={wr.h,nul.h};if(!UpdateProcThreadAttribute(attrs.p,0,PROC_THREAD_ATTRIBUTE_HANDLE_LIST,inherited,sizeof(inherited),nullptr,nullptr))return error();
 STARTUPINFOEXW si{};si.StartupInfo.cb=sizeof(si);si.StartupInfo.dwFlags=STARTF_USESTDHANDLES|STARTF_USESHOWWINDOW;si.StartupInfo.wShowWindow=SW_HIDE;si.StartupInfo.hStdInput=nul.h;si.StartupInfo.hStdOutput=wr.h;si.StartupInfo.hStdError=nul.h;si.lpAttributeList=attrs.p;
 PROCESS_INFORMATION pi{};if(!CreateProcessW(exe.c_str(),cmd.data(),nullptr,nullptr,TRUE,CREATE_NO_WINDOW|CREATE_SUSPENDED|EXTENDED_STARTUPINFO_PRESENT,nullptr,nullptr,&si.StartupInfo,&pi))return error();
 Handle process(pi.hProcess),thread(pi.hThread);if(!AssignProcessToJobObject(job.h,process.h)){HRESULT hr=error();TerminateProcess(process.h,1);return hr;}
 if(ResumeThread(thread.h)==DWORD(-1))return error();CloseHandle(wr.h);wr.h=nullptr;
 constexpr size_t limit=1024*1024*4+8;ULONGLONG deadline=GetTickCount64()+8000;out.clear();BYTE buffer[65536];
 bool exited=false;
 for(;;){if(GetTickCount64()>=deadline)return HRESULT_FROM_WIN32(ERROR_TIMEOUT);DWORD available=0;
  if(!PeekNamedPipe(rd.h,nullptr,0,nullptr,&available,nullptr)){if(GetLastError()==ERROR_BROKEN_PIPE)break;return error();}
  if(available){DWORD got=0;if(!ReadFile(rd.h,buffer,(DWORD)std::min<size_t>(sizeof(buffer),available),&got,nullptr))return error();if(out.size()+got>limit)return HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE);out.insert(out.end(),buffer,buffer+got);continue;}
  if(exited)break;
  if(WaitForSingleObject(process.h,10)==WAIT_OBJECT_0)exited=true;
 }
 ULONGLONG now=GetTickCount64();if(now>=deadline)return HRESULT_FROM_WIN32(ERROR_TIMEOUT);DWORD wait=WaitForSingleObject(process.h,(DWORD)(deadline-now));if(wait!=WAIT_OBJECT_0)return HRESULT_FROM_WIN32(ERROR_TIMEOUT);
 DWORD code=1;if(!GetExitCodeProcess(process.h,&code))return error();return code==0?S_OK:E_FAIL;
}
static HRESULT launch(const std::vector<std::wstring>&args){std::wstring exe=worker(),cmd=command(args);STARTUPINFOW si{};si.cb=sizeof(si);PROCESS_INFORMATION pi{};if(!CreateProcessW(exe.c_str(),cmd.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&si,&pi))return error();Handle p(pi.hProcess),t(pi.hThread);return S_OK;}
static uint32_t u32(const BYTE*p){return uint32_t(p[0])|(uint32_t(p[1])<<8)|(uint32_t(p[2])<<16)|(uint32_t(p[3])<<24);}
static HRESULT bitmap(const std::wstring&path,UINT size,bool thumb,HBITMAP*out){*out=nullptr;std::vector<BYTE> b;HRESULT hr=run({thumb?L"--thumbnail":L"--icon",std::to_wstring(std::max(1u,std::min(1024u,size))),path},b);if(FAILED(hr))return hr;if(b.size()<8)return E_FAIL;uint32_t w=u32(b.data()),h=u32(b.data()+4);if(!w||!h||w>1024||h>1024||b.size()!=8+size_t(w)*h*4)return E_FAIL;BITMAPINFO bi{};bi.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);bi.bmiHeader.biWidth=w;bi.bmiHeader.biHeight=-(LONG)h;bi.bmiHeader.biPlanes=1;bi.bmiHeader.biBitCount=32;bi.bmiHeader.biCompression=BI_RGB;void*p=nullptr;HBITMAP bm=CreateDIBSection(nullptr,&bi,DIB_RGB_COLORS,&p,nullptr,0);if(!bm)return error();memcpy(p,b.data()+8,b.size()-8);*out=bm;return S_OK;}
struct Gdi {HGDIOBJ h;~Gdi(){if(h)DeleteObject(h);}};
static HRESULT icon(const std::wstring&path,UINT size,HICON*out){HBITMAP bm=nullptr;HRESULT hr=bitmap(path,size,false,&bm);if(FAILED(hr))return hr;Gdi colorGuard{bm};BITMAP b{};GetObjectW(bm,sizeof(b),&b);std::vector<BYTE> zeros(((b.bmWidth+15)/16)*2*b.bmHeight,0);HBITMAP mask=CreateBitmap(b.bmWidth,b.bmHeight,1,1,zeros.data());ICONINFO ii{};ii.fIcon=TRUE;ii.hbmColor=bm;ii.hbmMask=mask;*out=mask?CreateIconIndirect(&ii):nullptr;if(mask)DeleteObject(mask);return *out?S_OK:E_FAIL;}
struct Snapshot{std::wstring path;Handle file;~Snapshot(){if(file.h&&file.h!=INVALID_HANDLE_VALUE){CloseHandle(file.h);file.h=nullptr;}if(!path.empty())DeleteFileW(path.c_str());}};
static HRESULT snapshot(IStream*s,Snapshot&tmp){STATSTG stat{};if(SUCCEEDED(s->Stat(&stat,STATFLAG_NONAME))&&stat.cbSize.QuadPart>512ull*1024*1024)return HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE);ULONGLONG deadline=GetTickCount64()+2000;wchar_t dir[32768];DWORD n=GetTempPathW(32768,dir);if(!n||n>=32768)return E_FAIL;GUID id;HRESULT hr=CoCreateGuid(&id);if(FAILED(hr))return hr;wchar_t name[40];StringFromGUID2(id,name,40);tmp.path=std::wstring(dir)+L"ApkShellext2-"+name+L".apk";tmp.file.h=CreateFileW(tmp.path.c_str(),GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,CREATE_NEW,FILE_ATTRIBUTE_TEMPORARY|FILE_FLAG_DELETE_ON_CLOSE,nullptr);if(tmp.file.h==INVALID_HANDLE_VALUE){tmp.path.clear();return error();}
 LARGE_INTEGER zero{};ULARGE_INTEGER old{};bool seekable=SUCCEEDED(s->Seek(zero,STREAM_SEEK_CUR,&old));if(seekable){hr=s->Seek(zero,STREAM_SEEK_SET,nullptr);if(FAILED(hr))return hr;}
 struct Restore{IStream*s;bool enabled;ULARGE_INTEGER old;~Restore(){if(enabled){LARGE_INTEGER p;p.QuadPart=old.QuadPart;s->Seek(p,STREAM_SEEK_SET,nullptr);}}} restore{s,seekable,old};
 BYTE buf[65536];uint64_t total=0;for(;;){if(GetTickCount64()>=deadline)return HRESULT_FROM_WIN32(ERROR_TIMEOUT);ULONG got=0;hr=s->Read(buf,sizeof(buf),&got);if(FAILED(hr))return hr;if(GetTickCount64()>=deadline)return HRESULT_FROM_WIN32(ERROR_TIMEOUT);total+=got;if(total>512ull*1024*1024)return HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE);if(got){DWORD written=0;if(!WriteFile(tmp.file.h,buf,got,&written,nullptr)||written!=got)return error();}if(hr==S_FALSE||!got)break;}if(!FlushFileBuffers(tmp.file.h))return error();return S_OK;}
#define GUARD try
#define END catch(HRESULT hr){return hr;}catch(const std::bad_alloc&){return E_OUTOFMEMORY;}catch(...){return E_FAIL;}
class Extension final:public IPersistFile,public IExtractIconW,public IExtractIconA,public IQueryInfo,public IInitializeWithStream,public IThumbnailProvider,public IShellExtInit,public IContextMenu{
 volatile LONG refs=1;int kind;std::wstring path;std::unique_ptr<Snapshot> input;HBITMAP cached=nullptr;bool initialized=false;std::vector<std::wstring> paths;
 public:explicit Extension(int k):kind(k){InterlockedIncrement(&objects);}~Extension(){if(cached)DeleteObject(cached);InterlockedDecrement(&objects);}
 HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void**p)noexcept override{if(!p)return E_POINTER;*p=nullptr;if(iid==IID_IUnknown)*p=static_cast<IPersistFile*>(this);else if((kind==0||kind==1)&&(iid==IID_IPersist||iid==IID_IPersistFile))*p=static_cast<IPersistFile*>(this);else if(kind==0&&iid==IID_IExtractIconW)*p=static_cast<IExtractIconW*>(this);else if(kind==0&&iid==IID_IExtractIconA)*p=static_cast<IExtractIconA*>(this);else if(kind==1&&iid==IID_IQueryInfo)*p=static_cast<IQueryInfo*>(this);else if(kind==2&&iid==__uuidof(IInitializeWithStream))*p=static_cast<IInitializeWithStream*>(this);else if(kind==2&&iid==__uuidof(IThumbnailProvider))*p=static_cast<IThumbnailProvider*>(this);else if(kind==3&&iid==IID_IShellExtInit)*p=static_cast<IShellExtInit*>(this);else if(kind==3&&iid==IID_IContextMenu)*p=static_cast<IContextMenu*>(this);else return E_NOINTERFACE;AddRef();return S_OK;}
 ULONG STDMETHODCALLTYPE AddRef()noexcept override{return InterlockedIncrement(&refs);}ULONG STDMETHODCALLTYPE Release()noexcept override{ULONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}
 HRESULT STDMETHODCALLTYPE GetClassID(CLSID*p)noexcept override{if(!p)return E_POINTER;*p=ids[kind];return S_OK;}HRESULT STDMETHODCALLTYPE IsDirty()noexcept override{return S_FALSE;}
 HRESULT STDMETHODCALLTYPE Load(LPCOLESTR p,DWORD)noexcept override GUARD{if(!p)return E_POINTER;path=p;return S_OK;}END
 HRESULT STDMETHODCALLTYPE Save(LPCOLESTR,BOOL)noexcept override{return E_NOTIMPL;}HRESULT STDMETHODCALLTYPE SaveCompleted(LPCOLESTR)noexcept override{return E_NOTIMPL;}
 HRESULT STDMETHODCALLTYPE GetCurFile(LPOLESTR*p)noexcept override{if(!p)return E_POINTER;*p=(LPWSTR)CoTaskMemAlloc((path.size()+1)*sizeof(wchar_t));if(!*p)return E_OUTOFMEMORY;memcpy(*p,path.c_str(),(path.size()+1)*sizeof(wchar_t));return S_OK;}
 HRESULT STDMETHODCALLTYPE GetIconLocation(UINT,LPWSTR p,UINT c,int*i,UINT*f)noexcept override{if(!p||!c||!i||!f)return E_INVALIDARG;p[0]=0;*i=0;*f=GIL_NOTFILENAME|GIL_PERINSTANCE;return S_OK;}
 HRESULT STDMETHODCALLTYPE GetIconLocation(UINT,LPSTR p,UINT c,int*i,UINT*f)noexcept override{if(!p||!c||!i||!f)return E_INVALIDARG;p[0]=0;*i=0;*f=GIL_NOTFILENAME|GIL_PERINSTANCE;return S_OK;}
 HRESULT STDMETHODCALLTYPE Extract(LPCWSTR,UINT,HICON*l,HICON*s,UINT sizes)noexcept override GUARD{if(l)*l=nullptr;if(s)*s=nullptr;struct Cleanup{HICON*l;HICON*s;bool okay=false;~Cleanup(){if(!okay){if(l&&*l){DestroyIcon(*l);*l=nullptr;}if(s&&*s){DestroyIcon(*s);*s=nullptr;}}}} cleanup{l,s};if(path.empty())return E_UNEXPECTED;HRESULT hr=S_OK;if(l)hr=icon(path,LOWORD(sizes),l);if(SUCCEEDED(hr)&&s)hr=icon(path,HIWORD(sizes),s);cleanup.okay=SUCCEEDED(hr);return hr;}END
 HRESULT STDMETHODCALLTYPE Extract(LPCSTR,UINT i,HICON*l,HICON*s,UINT z)noexcept override{return Extract((LPCWSTR)nullptr,i,l,s,z);}
 HRESULT STDMETHODCALLTYPE GetInfoTip(DWORD,LPWSTR*p)noexcept override GUARD{if(!p)return E_POINTER;*p=nullptr;std::vector<BYTE>b;HRESULT hr=run({L"--info",path},b);if(FAILED(hr))return hr;if(b.size()<4)return E_FAIL;uint32_t n=u32(b.data());if(n>32768||b.size()!=4+size_t(n)*2)return E_FAIL;*p=(LPWSTR)CoTaskMemAlloc((size_t(n)+1)*2);if(!*p)return E_OUTOFMEMORY;memcpy(*p,b.data()+4,n*2);(*p)[n]=0;return S_OK;}END
 HRESULT STDMETHODCALLTYPE GetInfoFlags(DWORD*p)noexcept override{if(!p)return E_POINTER;*p=0;return S_OK;}
 HRESULT STDMETHODCALLTYPE Initialize(IStream*s,DWORD)noexcept override GUARD{if(!s)return E_INVALIDARG;if(initialized)return HRESULT_FROM_WIN32(ERROR_ALREADY_INITIALIZED);auto temp=std::make_unique<Snapshot>();HRESULT hr=snapshot(s,*temp);if(FAILED(hr))return hr;input=std::move(temp);initialized=true;return S_OK;}END
 HRESULT STDMETHODCALLTYPE GetThumbnail(UINT size,HBITMAP*p,WTS_ALPHATYPE*a)noexcept override GUARD{if(!p||!a)return E_POINTER;*p=nullptr;*a=WTSAT_UNKNOWN;if(!initialized)return E_UNEXPECTED;if(!cached){if(!input)return E_UNEXPECTED;auto temp=std::move(input);HRESULT hr=bitmap(temp->path,size,true,&cached);if(FAILED(hr))return hr;}BITMAP b{};if(!GetObjectW(cached,sizeof(b),&b))return error();UINT maximum=std::max(1u,std::min(size,1024u));UINT largest=(UINT)std::max(b.bmWidth,b.bmHeight);maximum=std::min(maximum,largest);int w=std::max(1u,(UINT)b.bmWidth*maximum/largest),h=std::max(1u,(UINT)b.bmHeight*maximum/largest);*p=(HBITMAP)CopyImage(cached,IMAGE_BITMAP,w,h,LR_CREATEDIBSECTION);if(!*p)return error();*a=WTSAT_ARGB;return S_OK;}END
 HRESULT STDMETHODCALLTYPE Initialize(PCIDLIST_ABSOLUTE,IDataObject*data,HKEY)noexcept override GUARD{paths.clear();if(!data)return E_INVALIDARG;FORMATETC fmt{CF_HDROP,nullptr,DVASPECT_CONTENT,-1,TYMED_HGLOBAL};STGMEDIUM st{};HRESULT hr=data->GetData(&fmt,&st);if(FAILED(hr))return hr;struct Medium{STGMEDIUM*p;~Medium(){ReleaseStgMedium(p);}} m{&st};if(st.tymed!=TYMED_HGLOBAL)return E_INVALIDARG;HDROP drop=(HDROP)st.hGlobal;UINT count=DragQueryFileW(drop,0xffffffff,nullptr,0);if(count>128)return E_INVALIDARG;for(UINT i=0;i<count;++i){UINT n=DragQueryFileW(drop,i,nullptr,0);if(!n||n>=32768)return E_INVALIDARG;std::wstring p(n+1,0);if(DragQueryFileW(drop,i,p.data(),n+1)!=n)return E_FAIL;p.resize(n);paths.push_back(std::move(p));}return paths.empty()?E_INVALIDARG:S_OK;}END
 HRESULT STDMETHODCALLTYPE QueryContextMenu(HMENU menu,UINT pos,UINT first,UINT last,UINT flags)noexcept override{if(flags&CMF_DEFAULTONLY||paths.empty()||first>last)return MAKE_HRESULT(SEVERITY_SUCCESS,0,0);HMENU sub=CreatePopupMenu();if(!sub)return error();bool chinese=PRIMARYLANGID(GetUserDefaultUILanguage())==LANG_CHINESE;if(!AppendMenuW(sub,MF_STRING,first,chinese?L"安装包操作与设置…":L"Package actions and settings...")||!InsertMenuW(menu,pos,MF_BYPOSITION|MF_POPUP,(UINT_PTR)sub,chinese?L"APK Shell 扩展":L"APK Shell Extension")){DestroyMenu(sub);return error();}return MAKE_HRESULT(SEVERITY_SUCCESS,0,1);}
 HRESULT STDMETHODCALLTYPE InvokeCommand(LPCMINVOKECOMMANDINFO info)noexcept override GUARD{if(!info)return E_POINTER;if(info->cbSize>=sizeof(CMINVOKECOMMANDINFOEX)&&(info->fMask&CMIC_MASK_UNICODE)&&HIWORD(((const CMINVOKECOMMANDINFOEX*)info)->lpVerbW)){if(wcscmp(((const CMINVOKECOMMANDINFOEX*)info)->lpVerbW,L"apk-package-actions"))return E_INVALIDARG;}else if(HIWORD(info->lpVerb)){if(strcmp(info->lpVerb,"apk-package-actions"))return E_INVALIDARG;}else if(LOWORD(info->lpVerb)!=0)return E_INVALIDARG;if(paths.empty())return E_UNEXPECTED;std::vector<std::wstring>args{L"--menu"};args.insert(args.end(),paths.begin(),paths.end());return launch(args);}END
 HRESULT STDMETHODCALLTYPE GetCommandString(UINT_PTR id,UINT flags,UINT*,LPSTR text,UINT count)noexcept override{if(id)return E_INVALIDARG;if(flags==GCS_VALIDATEA||flags==GCS_VALIDATEW)return S_OK;if(!text||!count)return E_INVALIDARG;if(flags==GCS_VERBW||flags==GCS_HELPTEXTW){const wchar_t*s=flags==GCS_VERBW?L"apk-package-actions":L"Open APK package actions and settings";if(wcslen(s)>=count)return E_INVALIDARG;wcscpy((LPWSTR)text,s);return S_OK;}if(flags==GCS_VERBA||flags==GCS_HELPTEXTA){const char*s=flags==GCS_VERBA?"apk-package-actions":"Open APK package actions and settings";if(strlen(s)>=count)return E_INVALIDARG;strcpy(text,s);return S_OK;}return E_NOTIMPL;}
};
class Factory final:public IClassFactory{volatile LONG refs=1;int kind;public:explicit Factory(int k):kind(k){InterlockedIncrement(&objects);}~Factory(){InterlockedDecrement(&objects);}HRESULT STDMETHODCALLTYPE QueryInterface(REFIID i,void**p)noexcept override{if(!p)return E_POINTER;*p=nullptr;if(i!=IID_IUnknown&&i!=IID_IClassFactory)return E_NOINTERFACE;*p=this;AddRef();return S_OK;}ULONG STDMETHODCALLTYPE AddRef()noexcept override{return InterlockedIncrement(&refs);}ULONG STDMETHODCALLTYPE Release()noexcept override{ULONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown*outer,REFIID iid,void**p)noexcept override GUARD{if(!p)return E_POINTER;*p=nullptr;if(outer)return CLASS_E_NOAGGREGATION;auto*e=new Extension(kind);HRESULT hr=e->QueryInterface(iid,p);e->Release();return hr;}END HRESULT STDMETHODCALLTYPE LockServer(BOOL yes)noexcept override{if(yes)InterlockedIncrement(&locks);else InterlockedDecrement(&locks);return S_OK;}};
extern "C" __declspec(dllexport) HRESULT WINAPI DllGetClassObject(REFCLSID clsid,REFIID iid,void**p) GUARD{if(!p)return E_POINTER;*p=nullptr;for(int i=0;i<4;++i)if(clsid==ids[i]){auto*f=new Factory(i);HRESULT hr=f->QueryInterface(iid,p);f->Release();return hr;}return CLASS_E_CLASSNOTAVAILABLE;}END
extern "C" __declspec(dllexport) HRESULT WINAPI DllCanUnloadNow(){return objects==0&&locks==0?S_OK:S_FALSE;}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){module=h;DisableThreadLibraryCalls(h);}return TRUE;}
