using System.Runtime.InteropServices;

namespace BingBackground
{
	internal sealed class NativeMethods
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		internal static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
	}
}