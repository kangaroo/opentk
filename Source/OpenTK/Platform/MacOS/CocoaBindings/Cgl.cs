#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2010 the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

//  Created by Kenneth Pouncey on 2011/02/11.


using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenTK.Platform.MacOS
{
	#pragma warning disable 0169

	using CGLContext = IntPtr;

	unsafe static partial class Cgl
	{

		const string cgl = "/System/Library/Frameworks/OpenGL.framework/OpenGL";

		/*
         ** Error return values from GetError.
         */
		internal enum CglError
		{
			NoError = 0,
			/* no error                        */			InvalidEnum = 0x0500,
			InvalidValue = 0x0501,
			InvalidOperation = 0x0502,
			StackOverflow = 0x0503,
			StackUnderflow = 0x0504,
			OutOfMemory = 0x0505
		}

		public enum PixelFormatAttribute
		{
			AllRenderers = 1,
			DoubleBuffer = 5,
			Stereo = 6,
			AuxBuffers = 7,
			ColorSize = 8,
			AlphaSize = 11,
			DepthSize = 12,
			StencilSize = 13,
			AccumSize = 14,
			MinimumPolicy = 51,
			MaximumPolicy = 52,
			OffScreen = 53,
			FullScreen = 54,
			SampleBuffers = 55,
			Samples = 56,
			AuxDepthStencil = 57,
			ColorFloat = 58,
			Multisample = 59,
			Supersample = 60,
			SampleAlpha = 61,
			RendererID = 70,
			SingleRenderer = 71,
			NoRecovery = 72,
			Accelerated = 73,
			ClosestPolicy = 74,
			Robust = 75,
			BackingStore = 76,
			MPSafe = 78,
			Window = 80,
			MultiScreen = 81,
			Compliant = 83,
			ScreenMask = 84,
			PixelBuffer = 90,
			RemotePixelBuffer = 91,
			AllowOfflineRenderers = 96,
			AcceleratedCompute = 97,
			VirtualScreenCount = 128
		}

		[DllImport(cgl)]
		static internal extern uint glGetError ();
		public static CglError GetError ()
		{
			CglError ee = (CglError)glGetError ();
			return ee;
			
		}

		// No implementation for this yet.
		static internal string ErrorString (CglError code)
		{
			return string.Empty;
		}

		/*
         ** Current state functions
         */

		[DllImport(cgl, EntryPoint = "CGLSetCurrentContext")]
		static extern byte CGLSetCurrentContext (CGLContext ctx);
		static internal bool cglSetCurrentContext (IntPtr context)
		{
			byte retval = CGLSetCurrentContext (context);
			
			if (retval != 0)
				return false;
			else
				return true;
		}


		[DllImport(cgl)]
		static internal extern CGLContext CGLGetCurrentContext ();

		/*
		 **  Full Screen functions
		 */

		// Set full screen is actually implemented on the NSOpenGLContext
		//  what to do here.
		static internal void cglSetFullScreen (CGLContext ctx)
		{
			//int retval = CGLSetFullScreen(ctx);
			int retval = -1;
			
			if (retval == 0) {
				CglError err = GetError ();
				Debug.Print ("CGL Error: {0}", err);
				Debug.Indent ();
				Debug.Print (ErrorString (err));
				Debug.Unindent ();
				
				throw new MacOSException (err, ErrorString (err));
			}
		}

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [System.Runtime.InteropServices.DllImport(cgl)]
        internal extern static unsafe CglError CGLChoosePixelFormat(int[] attributes, IntPtr* pix, Int32* npix);
        public static 
        CglError ChoosePixelFormat(int[] attributes, out IntPtr pix, out int npix)
        {
            unsafe
            {
				fixed (IntPtr* pix_ptr = &pix)
                fixed (Int32* npix_ptr = &npix)
                {
                    CglError status = CGLChoosePixelFormat(attributes, pix_ptr, npix_ptr);
					pix = *pix_ptr;
					npix = *npix_ptr;
					return status;
                }
            }
        }
		
		[System.Security.SuppressUnmanagedCodeSecurity()]
        [System.Runtime.InteropServices.DllImport(cgl)]
        internal extern static CglError CGLDestroyPixelFormat(IntPtr pix);
			
        public static 
        CglError DestroyPixelFormat(IntPtr pix)
        {
			return CGLDestroyPixelFormat(pix);
        }	
		
		
		[System.Security.SuppressUnmanagedCodeSecurity()]
        [System.Runtime.InteropServices.DllImport(cgl)]
        internal extern static unsafe CglError CGLCreateContext(IntPtr pix, IntPtr share, IntPtr* ctx);

        public static 
        CglError CreateContext(IntPtr pix, IntPtr share, out IntPtr ctx)
        {
            unsafe
            {
				fixed (IntPtr* ctx_ptr = &ctx)
                {
                    CglError status = CGLCreateContext(pix, share, ctx_ptr);
					ctx = *ctx_ptr;
					return status;
                }
            }
        }
		
		#pragma warning restore 0169
	}
}
