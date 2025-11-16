/// Copyright (c)  2025  Xiaomi Corporation (authors: Fangjun Kuang)
using System;
using System.Runtime.InteropServices;
using System.Text;


namespace Eitan.SherpaONNXUnity.Runtime.Native
{
    public class VersionInfo
    {
        public static String Version
        {
            get
            {
                IntPtr p = SherpaONNXGetVersionStr();

                string s = "";
                int length = 0;

                unsafe
                {
                    byte* b = (byte*)p;
                    if (b != null)
                    {
                        while (*b != 0)
                        {
                            ++b;
                            length += 1;
                        }
                    }
                }

                if (length > 0)
                {
                    byte[] stringBuffer = new byte[length];
                    Marshal.Copy(p, stringBuffer, 0, length);
                    s = Encoding.UTF8.GetString(stringBuffer);
                }

                return s;
            }
        }

        public static String GitSha1
        {
            get
            {
                IntPtr p = SherpaONNXGetGitSha1();

                string s = "";
                int length = 0;

                unsafe
                {
                    byte* b = (byte*)p;
                    if (b != null)
                    {
                        while (*b != 0)
                        {
                            ++b;
                            length += 1;
                        }
                    }
                }

                if (length > 0)
                {
                    byte[] stringBuffer = new byte[length];
                    Marshal.Copy(p, stringBuffer, 0, length);
                    s = Encoding.UTF8.GetString(stringBuffer);
                }

                return s;
            }
        }

        public static String GitDate
        {
            get
            {
                IntPtr p = SherpaONNXGetGitDate();

                string s = "";
                int length = 0;

                unsafe
                {
                    byte* b = (byte*)p;
                    if (b != null)
                    {
                        while (*b != 0)
                        {
                            ++b;
                            length += 1;
                        }
                    }
                }

                if (length > 0)
                {
                    byte[] stringBuffer = new byte[length];
                    Marshal.Copy(p, stringBuffer, 0, length);
                    s = Encoding.UTF8.GetString(stringBuffer);
                }

                return s;
            }
        }


        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXGetVersionStr();

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXGetGitSha1();

        [DllImport(Dll.Filename)]
        private static extern IntPtr SherpaONNXGetGitDate();
    }
}
