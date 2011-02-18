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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenTK.Platform.MacOS
{
    using Carbon;
    using Graphics;

    using AGLRendererInfo = IntPtr;
    using CGLPixelFormat = IntPtr;
    using CGLContext = IntPtr;
    using AGLPbuffer = IntPtr;

    class CglContext : DesktopGraphicsContext
    {
        bool mVSync = false;
        // Todo: keep track of which display adapter was specified when the context was created.
        // IntPtr displayID;

        GraphicsMode graphics_mode;
        CocoaWindowInfo cocoaWindow;
        IntPtr shareContextRef;
        DisplayDevice device;
        bool mIsFullscreen = false;

        public CglContext(GraphicsMode mode, IWindowInfo window, IGraphicsContext shareContext)
        {
            Debug.Print("Context Type: {0}", shareContext);
            Debug.Print("Window info: {0}", window);
            
            this.graphics_mode = mode;
            this.cocoaWindow = (CocoaWindowInfo)window;
            
            if (shareContext is CglContext)
                shareContextRef = ((CglContext)shareContext).Handle.Handle;
            if (shareContext is GraphicsContext)
            {
                ContextHandle shareHandle = shareContext != null ? (shareContext as IGraphicsContextInternal).Context : (ContextHandle)IntPtr.Zero;
                
                shareContextRef = shareHandle.Handle;
            }
            
            if (shareContextRef == IntPtr.Zero)
            {
                Debug.Print("No context sharing will take place.");
            }
            
            CreateContext(mode, cocoaWindow, shareContextRef, true);
        }

        public CglContext(ContextHandle handle, IWindowInfo window, IGraphicsContext shareContext)
        {
            if (handle == ContextHandle.Zero)
                throw new ArgumentException("handle");
            if (window == null)
                throw new ArgumentNullException("window");
            
            Handle = handle;
            cocoaWindow = (CocoaWindowInfo)window;
            
			// Added by Kenneth
			// Don't forget this or the context will not register.  
			MakeCurrent(window);
        }


        private void AddPixelAttrib(List<int> cglAttributes, Cgl.PixelFormatAttribute pixelFormatAttribute)
        {
            Debug.Print(pixelFormatAttribute.ToString());
            
            cglAttributes.Add((int)pixelFormatAttribute);
        }
		
        private void AddPixelAttrib(List<int> cglAttributes, Cgl.PixelFormatAttribute pixelFormatAttribute, int value)
        {
            Debug.Print("{0} : {1}", pixelFormatAttribute, value);
            
            cglAttributes.Add((int)pixelFormatAttribute);
            cglAttributes.Add(value);
        }

		void CreateContext(GraphicsMode mode, CocoaWindowInfo cocoaWindow, IntPtr shareContextRef, bool fullscreen)
        {
            List<int> cglAttributes = new List<int>();
            
            Debug.Print("AGL pixel format attributes:");
            Debug.Indent();
            
            AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.Accelerated);
            AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.NoRecovery);
            AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.DoubleBuffer);
            AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.ColorSize, 24);
            AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.DepthSize, 16);
            
            if (mode.Depth > 0)
                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.DepthSize, mode.Depth);
            
            if (mode.Stencil > 0)
                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.StencilSize, mode.Stencil);
            
//            if (mode.AccumulatorFormat.BitsPerPixel > 0)
//            {
//                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.AGL_ACCUM_RED_SIZE, mode.AccumulatorFormat.Red);
//                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.AGL_ACCUM_GREEN_SIZE, mode.AccumulatorFormat.Green);
//                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.AGL_ACCUM_BLUE_SIZE, mode.AccumulatorFormat.Blue);
//                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.AGL_ACCUM_ALPHA_SIZE, mode.AccumulatorFormat.Alpha);
//            }
//            
            if (mode.Samples > 1)
            {
                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.SampleBuffers, 1);
                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.Samples, mode.Samples);
            }
            
            if (fullscreen)
            {
                AddPixelAttrib(cglAttributes, Cgl.PixelFormatAttribute.FullScreen);
            }
            
            Debug.Unindent();
            
            Debug.Write("Attribute array:  ");
            for (int i = 0; i < cglAttributes.Count; i++)
                Debug.Write(cglAttributes[i].ToString() + "  ");
            Debug.WriteLine("");
            
            CGLPixelFormat myCGLPixelFormat;
            int nPix = 0;
			
            // Choose a pixel format with the attributes we specified.
            if (fullscreen)
            {
                Cgl.CglError status = Cgl.ChoosePixelFormat(cglAttributes.ToArray(), out myCGLPixelFormat, out nPix);
                
                if (status != Cgl.CglError.NoError)
                {
                    Debug.Print("Failed to create full screen pixel format.");
                    Debug.Print("Trying again to create a non-fullscreen pixel format.");
                    
                    CreateContext(mode, cocoaWindow, shareContextRef, false);
                    return;
                }
            }

            else
            {
                Cgl.CglError status = Cgl.ChoosePixelFormat(cglAttributes.ToArray(), out myCGLPixelFormat, out nPix);
                
                MyCGLReportError("cglChoosePixelFormat");
            }
            
            
            Debug.Print("Creating AGL context.  Sharing with {0}", shareContextRef);
            
            // create the context and share it with the share reference.
			IntPtr ctx = IntPtr.Zero;
			Cgl.CglError crStatus = Cgl.CreateContext(myCGLPixelFormat, shareContextRef, out ctx);
			
            Handle = new ContextHandle(ctx);
			
            MyCGLReportError("cglCreateContext");
            
            // Free the pixel format from memory.
            Cgl.DestroyPixelFormat(myCGLPixelFormat);
            MyCGLReportError("cglDestroyPixelFormat");
            
            Debug.Print("IsControl: {0}", cocoaWindow.IsControl);
            
            //SetDrawable(cocoaWindow);
            //SetBufferRect(cocoaWindow);
            //Update(cocoaWindow);
            
            MakeCurrent(cocoaWindow);
            
            Debug.Print("context: {0}", Handle.Handle);
        }

        private IntPtr GetQuartzDevice(CocoaWindowInfo cocoaWindow)
        {
            IntPtr windowRef = cocoaWindow.WindowRef;
            
            if (CocoaGLNative.WindowRefMap.ContainsKey(windowRef) == false)
                return IntPtr.Zero;
            
            WeakReference nativeRef = CocoaGLNative.WindowRefMap[windowRef];
            if (nativeRef.IsAlive == false)
                return IntPtr.Zero;
            
            CocoaGLNative window = nativeRef.Target as CocoaGLNative;
            
            if (window == null)
                return IntPtr.Zero;
            
            return QuartzDisplayDeviceDriver.HandleTo(window.TargetDisplayDevice);
            
        }

        void SetBufferRect(CocoaWindowInfo carbonWindow)
        {
            if (carbonWindow.IsControl == false)
                return;

            // Todo: See if there is a way around using WinForms.
            throw new NotImplementedException();
#if false
            System.Windows.Forms.Control ctrl = Control.FromHandle(carbonWindow.WindowRef);
            
            if (ctrl.TopLevelControl == null)
                return;
            
            Rect rect = API.GetControlBounds(carbonWindow.WindowRef);
            System.Windows.Forms.Form frm = (System.Windows.Forms.Form)ctrl.TopLevelControl;
            
            System.Drawing.Point loc = frm.PointToClient(ctrl.PointToScreen(System.Drawing.Point.Empty));
            
            rect.X = (short)loc.X;
            rect.Y = (short)loc.Y;
            
            Debug.Print("Setting buffer_rect for control.");
            Debug.Print("MacOS Coordinate Rect:   {0}", rect);
            
            rect.Y = (short)(ctrl.TopLevelControl.ClientSize.Height - rect.Y - rect.Height);
            Debug.Print("  AGL Coordinate Rect:   {0}", rect);
            
            int[] glrect = new int[4];
            
            glrect[0] = rect.X;
            glrect[1] = rect.Y;
            glrect[2] = rect.Width;
            glrect[3] = rect.Height;
            
            Agl.aglSetInteger(Handle.Handle, Agl.ParameterNames.AGL_BUFFER_RECT, glrect);
            MyAGLReportError("aglSetInteger");
            
            Agl.aglEnable(Handle.Handle, Agl.ParameterNames.AGL_BUFFER_RECT);
            MyAGLReportError("aglEnable");
#endif
        }
        void SetDrawable(CocoaWindowInfo carbonWindow)
        {
            IntPtr windowPort = GetWindowPortForWindowInfo(carbonWindow);
            //Debug.Print("Setting drawable for context {0} to window port: {1}", Handle.Handle, windowPort);
            
            Agl.aglSetDrawable(Handle.Handle, windowPort);
            
            MyCGLReportError("aglSetDrawable");
            
        }

        private static IntPtr GetWindowPortForWindowInfo(CocoaWindowInfo carbonWindow)
        {
            IntPtr windowPort;
            if (carbonWindow.IsControl)
            {
                IntPtr controlOwner = API.GetControlOwner(carbonWindow.WindowRef);
                
                windowPort = API.GetWindowPort(controlOwner);
            }

            else
                windowPort = API.GetWindowPort(carbonWindow.WindowRef);
            
            return windowPort;
        }
		
        public override void Update(IWindowInfo window)
        {
            CocoaWindowInfo carbonWindow = (CocoaWindowInfo)window;
            
            if (carbonWindow.GoFullScreenHack)
            {
                carbonWindow.GoFullScreenHack = false;
                CocoaGLNative wind = GetCocoaWindow(carbonWindow);
                
                if (wind != null)
                    wind.SetFullscreen(this);
                else
                    Debug.Print("Could not find window!");
                
                return;
            }

            else if (carbonWindow.GoWindowedHack)
            {
                carbonWindow.GoWindowedHack = false;
                CocoaGLNative wind = GetCocoaWindow(carbonWindow);
                
                if (wind != null)
                    wind.UnsetFullscreen(this);
                else
                    Debug.Print("Could not find window!");
                
            }
            
            if (mIsFullscreen)
                return;
            
            SetDrawable(carbonWindow);
            SetBufferRect(carbonWindow);
            
            Agl.aglUpdateContext(Handle.Handle);
        }

        private CocoaGLNative GetCocoaWindow(CocoaWindowInfo cocoaWindow)
        {
            WeakReference r = CocoaGLNative.WindowRefMap[cocoaWindow.WindowRef];
            
            if (r.IsAlive)
            {
                return (CocoaGLNative)r.Target;
            }

            else
                return null;
        }

        void MyCGLReportError(string function)
        {
            Cgl.CglError err = Cgl.GetError();
            
            if (err != Cgl.CglError.NoError)
                throw new MacOSException((OSStatus)err, string.Format("CGL Error from function {0}: {1}  {2}", function, err, Cgl.ErrorString(err)));
        }

        bool firstFullScreen = false;

        internal void SetFullScreen(CocoaWindowInfo info, out int width, out int height)
        {
            CocoaGLNative wind = GetCocoaWindow(info);
            
            Debug.Print("Switching to full screen {0}x{1} on context {2}", wind.TargetDisplayDevice.Width, wind.TargetDisplayDevice.Height, Handle.Handle);
            
            CG.DisplayCapture(GetQuartzDevice(info));
            Cgl.cglSetFullScreen(Handle.Handle);
            MakeCurrent(info);
            
            width = wind.TargetDisplayDevice.Width;
            height = wind.TargetDisplayDevice.Height;
            
            // This is a weird hack to workaround a bug where the first time a context
            // is made fullscreen, we just end up with a blank screen.  So we undo it as fullscreen
            // and redo it as fullscreen.  
//            if (firstFullScreen == false)
//            {
//                firstFullScreen = true;
//                UnsetFullScreen(info);
//                SetFullScreen(info, out width, out height);
//            }
            
            mIsFullscreen = true;
        }
		
        internal void UnsetFullScreen(CocoaWindowInfo windowInfo)
        {
            Debug.Print("Unsetting AGL fullscreen.");
            Agl.aglSetDrawable(Handle.Handle, IntPtr.Zero);
            Agl.aglUpdateContext(Handle.Handle);
            
            CG.DisplayRelease(GetQuartzDevice(windowInfo));
            Debug.Print("Resetting drawable.");
            SetDrawable(windowInfo);
            
            mIsFullscreen = false;
        }


        #region IGraphicsContext Members

        bool firstSwap = false;
        public override void SwapBuffers()
        {
            // this is part of the hack to avoid dropping the first frame when
            // using multiple GLControls.
            if (firstSwap == false && cocoaWindow.IsControl)
            {
                Debug.WriteLine("--> Resetting drawable. <--");
                firstSwap = true;
                SetDrawable(cocoaWindow);
                Update(cocoaWindow);
            }
            
            Agl.aglSwapBuffers(Handle.Handle);
            MyCGLReportError("aglSwapBuffers");
        }

        public override void MakeCurrent(IWindowInfo window)
        {
            if (Cgl.cglSetCurrentContext(Handle.Handle) == false)
                MyCGLReportError("aglSetCurrentContext");
        }

        public override bool IsCurrent
        {
            get { return (Handle.Handle == Cgl.CGLGetCurrentContext()); }
        }

        public override bool VSync
        {
            get { return mVSync; }
            set
            {
                int intVal = value ? 1 : 0;
                
                Agl.aglSetInteger(Handle.Handle, Agl.ParameterNames.AGL_SWAP_INTERVAL, ref intVal);
                
                mVSync = value;
            }
        }

        #endregion

        #region IDisposable Members

        ~CglContext()
        {
            Dispose(false);
        }

        public override void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (IsDisposed || Handle.Handle == IntPtr.Zero)
                return;
            
            Debug.Print("Disposing of AGL context.");
            Agl.aglSetCurrentContext(IntPtr.Zero);
            
            //Debug.Print("Setting drawable to null for context {0}.", Handle.Handle);
            //Agl.aglSetDrawable(Handle.Handle, IntPtr.Zero);
            
            // I do not know MacOS allows us to destroy a context from a separate thread,
            // like the finalizer thread.  It's untested, but worst case is probably
            // an exception on application exit, which would be logged to the console.
            Debug.Print("Destroying context");
            if (Agl.aglDestroyContext(Handle.Handle) == true)
            {
                Debug.Print("Context destruction completed successfully.");
                Handle = ContextHandle.Zero;
                return;
            }
            
            // failed to destroy context.
            Debug.WriteLine("Failed to destroy context.");
            Debug.WriteLine(Agl.ErrorString(Agl.GetError()));
            
            // don't throw an exception from the finalizer thread.
            if (disposing)
            {
                throw new MacOSException((OSStatus)Agl.GetError(), Agl.ErrorString(Agl.GetError()));
            }
            
            IsDisposed = true;
        }

        #endregion

        #region IGraphicsContextInternal Members

        private const string Library = "libdl.dylib";

        [DllImport(Library, EntryPoint = "NSIsSymbolNameDefined")]
        private static extern bool NSIsSymbolNameDefined(string s);
        [DllImport(Library, EntryPoint = "NSLookupAndBindSymbol")]
        private static extern IntPtr NSLookupAndBindSymbol(string s);
        [DllImport(Library, EntryPoint = "NSAddressOfSymbol")]
        private static extern IntPtr NSAddressOfSymbol(IntPtr symbol);

        public override IntPtr GetAddress(string function)
        {
            string fname = "_" + function;
            if (!NSIsSymbolNameDefined(fname))
                return IntPtr.Zero;
            
            IntPtr symbol = NSLookupAndBindSymbol(fname);
            if (symbol != IntPtr.Zero)
                symbol = NSAddressOfSymbol(symbol);
            
            return symbol;
        }
        
        #endregion
    }
}
