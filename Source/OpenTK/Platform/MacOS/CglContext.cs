#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2010 the Open Toolkit library.
// Copyrigth (c) 2008 Erik Ylvisaker
// Copyright (c) 2011 Geoff Norton
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenTK.Platform.MacOS
{
    using Graphics;

    /*
     * OpenTK has a bunch of plumbing presuming a number of things about the native
     * windowing control system and the fact that they are unaware of OpenGL
     * 
     * Cocoa is aware of OpenGL thru the NSOpenGLContext, so this is a dummy class
     * that solely exists so that we can properly connect the delegates
     */

    class CglContext : DesktopGraphicsContext
    {
        public CglContext()
        {
            Handle = new ContextHandle ((IntPtr) 0xdeadbeef);
        }

        #region IGraphicsContext Members

        public override void SwapBuffers ()
        {
            throw new NotImplementedException ();
        }

        public override void MakeCurrent (IWindowInfo window)
        {
            /* 
             * We do not support setting the current context here, since the caller should handle that
             * thru NSOpenGLContext.MakeCurrentContext ();
             * However, we cannot throw here since the startup plumbing requires a current GraphicsContext
             * which in turn calls this, and GraphicsContext is sealed so we cannot inherit from it.
             */
        }

        public override bool IsCurrent
        {
            get { throw new NotImplementedException (); }
        }

        public override bool VSync
        {
            get { throw new NotImplementedException (); }
            set { throw new NotImplementedException (); }
        }

        public override void Dispose ()
        {
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
