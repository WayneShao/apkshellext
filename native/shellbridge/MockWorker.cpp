// Protocol-only worker for native bridge regression; never deploy this executable.
#define UNICODE
#include <windows.h>
#include <string>
#include <vector>
#include <cstdint>
static bool output(const void*data,DWORD length){DWORD written=0;return WriteFile(GetStdHandle(STD_OUTPUT_HANDLE),data,length,&written,nullptr)&&written==length;}
int wmain(int argc,wchar_t**argv){
 if(argc<3)return 2;
 std::wstring path=argv[argc-1];
 if(path.find(L"timeout")!=std::wstring::npos){Sleep(30000);return 1;}
 if(path.find(L"oversize")!=std::wstring::npos){std::vector<char>b(65536);for(int i=0;i<70;++i)if(!output(b.data(),(DWORD)b.size()))return 1;return 0;}
 if(path.find(L"truncated")!=std::wstring::npos){uint32_t header[]={64,64};output(header,sizeof(header));return 0;}
 HANDLE f=CreateFileW(path.c_str(),GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,0,nullptr);
 if(f==INVALID_HANDLE_VALUE)return 3;CloseHandle(f);
 if(std::wstring(argv[1])==L"--info"){std::wstring text=L"APK protocol fixture";uint32_t n=(uint32_t)text.size();return output(&n,4)&&output(text.data(),n*2)?0:4;}
 uint32_t n=(uint32_t)_wtoi(argv[2]);uint32_t header[]={n,n};std::vector<uint32_t>pixels(n*n,0xff4488cc);return output(header,8)&&output(pixels.data(),n*n*4)?0:5;
}
