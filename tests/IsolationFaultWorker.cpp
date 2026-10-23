#include <windows.h>
int main(){
#ifdef HANG_WORKER
Sleep(INFINITE);
#endif
return 23;
}
