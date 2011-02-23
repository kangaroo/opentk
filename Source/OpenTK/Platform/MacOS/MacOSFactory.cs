#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library.
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

using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Input;

namespace OpenTK.Platform.MacOS
{
    using Graphics;

    class MacOSFactory : IPlatformFactory
    {

        public void Initialize ()
        {
#if MONOMAC
            GraphicsContext ctx = GraphicsContext.CreateMonoMacContext ();

            ctx.MakeCurrent (null);
            new OpenTK.Graphics.OpenGL.GL ().LoadEntryPoints ();

            // We leak this for the duration of the run so that the weakref keeps it alive
            System.Runtime.InteropServices.GCHandle.Alloc (ctx);
#endif
        }

        #region Fields

#if !MONOMAC
        readonly IInputDriver2 InputDriver = new HIDInput();
#endif

        #endregion

        #region IPlatformFactory Members

        public virtual INativeWindow CreateNativeWindow(int x, int y, int width, int height, string title, GraphicsMode mode, GameWindowFlags options, DisplayDevice device)
        {
            return new CarbonGLNative(x, y, width, height, title, mode, options, device);
        }

        public virtual IDisplayDeviceDriver CreateDisplayDeviceDriver()
        {
            return new QuartzDisplayDeviceDriver();
        }

        public virtual IGraphicsContext CreateGLContext(GraphicsMode mode, IWindowInfo window, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
        {
            return new AglContext(mode, window, shareContext);
        }

        public virtual IGraphicsContext CreateGLContext(ContextHandle handle, IWindowInfo window, IGraphicsContext shareContext, bool directRendering, int major, int minor, GraphicsContextFlags flags)
        {
            return new AglContext(handle, window, shareContext);
        }

        public virtual GraphicsContext.GetCurrentContextDelegate CreateGetCurrentGraphicsContext()
        {
            return (GraphicsContext.GetCurrentContextDelegate)delegate
            {
#if MONOMAC
                return new ContextHandle((IntPtr) 0xdeadbeef);
#else
                return new ContextHandle(Agl.aglGetCurrentContext());
#endif
            };
        }

        public virtual IGraphicsMode CreateGraphicsMode()
        {
            return new MacOSGraphicsMode();
        }

        public virtual OpenTK.Input.IKeyboardDriver2 CreateKeyboardDriver()
        {
#if MONOMAC
           throw new NotImplementedException ();
#else
           return InputDriver.KeyboardDriver;
#endif
        }

        public virtual OpenTK.Input.IMouseDriver2 CreateMouseDriver()
        {
#if MONOMAC
           throw new NotImplementedException ();
#else
            return InputDriver.MouseDriver;
#endif
        }
        
        #endregion
    }
}
