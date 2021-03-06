﻿#region License
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Bind.Structures;

namespace Bind
{
    using Delegate = Bind.Structures.Delegate;
    using Enum = Bind.Structures.Enum;
    using Type = Bind.Structures.Type;

    sealed class CppSpecWriter : ISpecWriter
    {
        readonly char[] numbers = "0123456789".ToCharArray();
        const string AllowDeprecated = "ALLOW_DEPRECATED_GL";
        const string DigitPrefix = "T"; // Prefix for identifiers that start with a digit
        const string OutputFileCpp = "glcore++.cpp";
        const string OutputFileHeader = "glcore++.h";
        const string OutputFileHeaderCompat = "glcompat++.h";
        const string OutputFileHeaderEnums = "glenums++.h";

        BindStreamWriter sw_h = new BindStreamWriter(Path.GetTempFileName());
        BindStreamWriter sw_cpp = new BindStreamWriter(Path.GetTempFileName());
        BindStreamWriter sw_h_compat = new BindStreamWriter(Path.GetTempFileName());
        BindStreamWriter sw_cpp_compat = new BindStreamWriter(Path.GetTempFileName());
        BindStreamWriter sw_h_enums = new BindStreamWriter(Path.GetTempFileName());

        #region WriteBindings

        public void WriteBindings(IBind generator)
        {
            WriteBindings(generator.Delegates, generator.Wrappers, generator.Enums);
        }

        void WriteBindings(DelegateCollection delegates, FunctionCollection wrappers, EnumCollection enums)
        {
            Console.WriteLine("Writing bindings to {0}", Settings.OutputPath);
            if (!Directory.Exists(Settings.OutputPath))
                Directory.CreateDirectory(Settings.OutputPath);

            // Hack: Fix 3dfx extension category so it doesn't start with a digit
            if (wrappers.ContainsKey("3dfx"))
            {
                var three_dee_fx = wrappers["3dfx"];
                wrappers.Remove("3dfx");
                wrappers.Add(DigitPrefix + "3dfx", three_dee_fx);
            }

            Settings.DefaultOutputNamespace = "OpenTK";

            // Enums
            using (var sw = sw_h_enums)
            {
                WriteEnums(sw, enums);
                sw.Flush();
                sw.Close();
            }

            // Core definitions
            using (var sw = sw_h)
            {
                WriteDefinitions(sw, enums, wrappers, Type.CSTypes, false);
                sw.Flush();
                sw.Close();
            }

            // Compatibility definitions
            using (var sw = sw_h_compat)
            {
                WriteDefinitions(sw, enums, wrappers, Type.CSTypes, true);
                sw.Flush();
                sw.Close();
            }

            // Core & compatibility declarations
            using (var sw = sw_cpp)
            {
                WriteDeclarations(sw, wrappers, Type.CSTypes);
                sw.Flush();
                sw.Close();
            }

            string output_header = Path.Combine(Settings.OutputPath, OutputFileHeader);
            string output_cpp = Path.Combine(Settings.OutputPath, OutputFileCpp);
            string output_header_compat = Path.Combine(Settings.OutputPath, OutputFileHeaderCompat);
            string output_header_enums = Path.Combine(Settings.OutputPath, OutputFileHeaderEnums);

            Move(sw_h.File, output_header);
            Move(sw_cpp.File, output_cpp);
            Move(sw_h_compat.File, output_header_compat);
            Move(sw_h_enums.File, output_header_enums);
        }

        void Move(string file, string dest)
        {
            if (File.Exists(dest))
                File.Delete(dest);
            File.Move(file, dest);
        }

        #endregion

        #region WriteDefinitions

        void WriteDefinitions(BindStreamWriter sw,
            EnumCollection enums, FunctionCollection wrappers,
            Dictionary<string, string> CSTypes, bool deprecated_only)
        {
            sw.WriteLine("#pragma once");
            if (!deprecated_only)
            {
                sw.WriteLine("#ifndef GLCOREPP_H");
                sw.WriteLine("#define GLCOREPP_H");
            }
            else
            {
                sw.WriteLine("#ifndef GLCOMPATPP_H");
                sw.WriteLine("#define GLCOMPATPP_H");
            }
            sw.WriteLine();
            WriteLicense(sw);

            sw.WriteLine("namespace {0}", Settings.OutputNamespace);
            sw.WriteLine("{");
            sw.Indent();
            sw.WriteLine("namespace {0}", Settings.GLClass);
            sw.WriteLine("{");
            sw.Indent();

            foreach (string extension in wrappers.Keys)
            {
                if (extension != "Core")
                {
                    sw.WriteLine("namespace {0}", extension);
                    sw.WriteLine("{");
                    sw.Indent();
                }

                // Avoid multiple definitions of the same function
                Delegate last_delegate = null;

                // Write delegates
                sw.WriteLine("namespace Delegates");
                sw.WriteLine("{");
                sw.Indent();
                var functions = wrappers[extension].Where(f => f.Deprecated == deprecated_only);
                last_delegate = null;
                foreach (var f in functions)
                {
                    WriteDelegate(sw, f.WrappedDelegate, ref last_delegate);
                }

                sw.Unindent();
                sw.WriteLine("};");

                // Write wrappers
                sw.WriteLine("void Init();");
                last_delegate = null;
                foreach (var f in functions)
                {
                    if (last_delegate == f.WrappedDelegate)
                        continue;
                    last_delegate = f.WrappedDelegate;

                    var parameters = f.WrappedDelegate.Parameters.ToString()
                        .Replace("String[]", "String*")
                        .Replace("[OutAttribute]", String.Empty);
                    sw.WriteLine("inline {0} {1}{2}", f.WrappedDelegate.ReturnType,
                        f.TrimmedName, parameters);
                    sw.WriteLine("{");
                    sw.Indent();
                    WriteMethodBody(sw, f);
                    sw.Unindent();
                    sw.WriteLine("}");
                }

                if (extension != "Core")
                {
                    sw.Unindent();
                    sw.WriteLine("};");
                }
            }

            sw.Unindent();
            sw.WriteLine("};");

            sw.Unindent();
            sw.WriteLine("}");

            sw.WriteLine("#endif");
        }

        #endregion

        #region WriteDeclarations

        void WriteDeclarations(BindStreamWriter sw, FunctionCollection wrappers,
           Dictionary<string, string> CSTypes)
        {
            sw.WriteLine("#ifdef GLPP_INTERNAL_COMPILE_DECLARATIONS");

            WriteLicense(sw);

            sw.WriteLine("namespace {0}", Settings.OutputNamespace);
            sw.WriteLine("{");
            sw.Indent();

            sw.WriteLine("using namespace Internals;");

            // Used to avoid multiple declarations of the same function
            Delegate last_delegate = null;

            // Declare all functions (deprecated and core).
            // This is necessary for projects that wish to move from
            // deprecated APIs to core piece-by-piece.
            foreach (var ext in wrappers.Keys)
            {
                last_delegate = null;
                var functions = wrappers[ext];
                foreach (var function in functions)
                {
                    if (function.WrappedDelegate == last_delegate)
                        continue;
                    last_delegate = function.WrappedDelegate;

                    string path = GetNamespace(ext);
                    sw.WriteLine("{0}::Delegates::p{1} {0}::Delegates::{1} = 0;", path, function.Name);
                }
            }
            sw.WriteLine();

            // Add Init() methods
            foreach (var ext in wrappers.Keys)
            {
                string path = GetNamespace(ext);

                sw.WriteLine("void {0}::Init()", path);
                sw.WriteLine("{");
                sw.Indent();

                last_delegate = null;
                var functions = wrappers[ext];
                foreach (var function in functions)
                {
                    if (function.WrappedDelegate == last_delegate)
                        continue;
                    last_delegate = function.WrappedDelegate;

                    sw.WriteLine("{0}::Delegates::{1} = ({0}::Delegates::p{1})GetAddress(\"gl{1}\");",
                         path, function.WrappedDelegate.Name);
                }
                sw.Unindent();
                sw.WriteLine("}");
            }

            sw.Unindent();
            sw.WriteLine("}");

            sw.WriteLine("#endif");
        }

        static string GetNamespace(string ext)
        {
            if (ext == "Core")
                return Settings.GLClass;
            else
                return String.Format("{0}::{1}", Settings.GLClass, Char.IsDigit(ext[0]) ? DigitPrefix + ext : ext);
        }

        #endregion

        #region ISpecWriter Members

        #region WriteEnums

        public void WriteEnums(BindStreamWriter sw, EnumCollection enums)
        {
            sw.WriteLine("#pragma once");
            sw.WriteLine("#ifndef GLENUMSPP_H");
            sw.WriteLine("#define GLENUMSPP_H");
            sw.WriteLine();
            WriteLicense(sw);

            sw.WriteLine("namespace {0}", Settings.OutputNamespace);
            sw.WriteLine("{");
            sw.Indent();

            foreach (Enum @enum in enums.Values)
            {
                sw.WriteLine("struct {0} : Enumeration<{0}>", @enum.Name);
                sw.WriteLine("{");
                sw.Indent();
                sw.WriteLine("inline {0}(int value) : Enumeration<{0}>(value) {{ }}", @enum.Name);
                sw.WriteLine("enum");
                sw.WriteLine("{");
                sw.Indent();
                foreach (var c in @enum.ConstantCollection.Values)
                {
                    // C++ doesn't have the concept of "unchecked", so remove this.
                    if (!c.Unchecked)
                        sw.WriteLine("{0},", c);
                    else
                        sw.WriteLine("{0},", c.ToString().Replace("unchecked", String.Empty));
                }
                sw.Unindent();
                sw.WriteLine("};");
                sw.Unindent();
                sw.WriteLine("};");
                sw.WriteLine();
            }

            sw.Unindent();
            sw.WriteLine("}");

            sw.WriteLine("#endif");
        }

        #endregion

        #region WriteTypes

        public void WriteTypes(BindStreamWriter sw, Dictionary<string, string> CSTypes)
        {
            sw.WriteLine();
            foreach (string s in CSTypes.Keys)
            {
                sw.WriteLine("typedef {0} {1};", s, CSTypes[s]);
            }
        }

        #endregion

        #region WriteDelegates

        public void WriteDelegates(BindStreamWriter sw, DelegateCollection delegates)
        {
            throw new NotSupportedException();
        }

        static void WriteDelegate(BindStreamWriter sw, Delegate d, ref Delegate last_delegate)
        {
            // Avoid multiple definitions of the same function
            if (d != last_delegate)
            {
                last_delegate = d;
                var parameters = d.Parameters.ToString()
                    .Replace("String[]", "String*")
                    .Replace("[OutAttribute]", String.Empty);
                sw.WriteLine("typedef {0} (APIENTRY *p{1}){2};", d.ReturnType, d.Name, parameters);
                sw.WriteLine("extern p{0} {0};", d.Name);
            }
        }

        #endregion

        #region WriteImports

        public void WriteImports(BindStreamWriter sw, DelegateCollection delegates)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region WriteWrappers

        public void WriteWrappers(BindStreamWriter sw, FunctionCollection wrappers,
            Dictionary<string, string> CSTypes)
        {
            throw new NotSupportedException();
        }

        static void WriteMethodBody(BindStreamWriter sw, Function f)
        {
            var callstring = f.Parameters.CallString()
                .Replace("String[]", "String*");
            if (f.ReturnType != null && !f.ReturnType.ToString().ToLower().Contains("void"))
                sw.WriteLine("return Delegates::{0}{1};", f.WrappedDelegate.Name, callstring);
            else
                sw.WriteLine("Delegates::{0}{1};", f.WrappedDelegate.Name, callstring);
        }

        static DocProcessor processor = new DocProcessor(Path.Combine(Settings.DocPath, Settings.DocFile));
        static Dictionary<string, string> docfiles;
        void WriteDocumentation(BindStreamWriter sw, Function f)
        {
            if (docfiles == null)
            {
                docfiles = new Dictionary<string, string>();
                foreach (string file in Directory.GetFiles(Settings.DocPath))
                {
                    docfiles.Add(Path.GetFileName(file), file);
                }
            }

            string docfile = null;
            try
            {
                docfile = Settings.FunctionPrefix + f.WrappedDelegate.Name + ".xml";
                if (!docfiles.ContainsKey(docfile))
                    docfile = Settings.FunctionPrefix + f.TrimmedName + ".xml";
                if (!docfiles.ContainsKey(docfile))
                    docfile = Settings.FunctionPrefix + f.TrimmedName.TrimEnd(numbers) + ".xml";

                string doc = null;
                if (docfiles.ContainsKey(docfile))
                {
                    doc = processor.ProcessFile(docfiles[docfile]);
                }
                if (doc == null)
                {
                    doc = "/// <summary></summary>";
                }

                int summary_start = doc.IndexOf("<summary>") + "<summary>".Length;
                string warning = "[deprecated: v{0}]";
                string category = "[requires: {0}]";
                if (f.Deprecated)
                {
                    warning = String.Format(warning, f.DeprecatedVersion);
                    doc = doc.Insert(summary_start, warning);
                }

                if (f.Extension != "Core" && !String.IsNullOrEmpty(f.Category))
                {
                    category = String.Format(category, f.Category);
                    doc = doc.Insert(summary_start, category);
                }
                else if (!String.IsNullOrEmpty(f.Version))
                {
                    if (f.Category.StartsWith("VERSION"))
                        category = String.Format(category, "v" + f.Version);
                    else
                        category = String.Format(category, "v" + f.Version + " and " + f.Category);
                    doc = doc.Insert(summary_start, category);
                }

                sw.WriteLine(doc);
            }
            catch (Exception e)
            {
                Console.WriteLine("[Warning] Error processing file {0}: {1}", docfile, e.ToString());
            }
        }

        #endregion

        #endregion

        #region WriteLicense

        public void WriteLicense(BindStreamWriter sw)
        {
            sw.WriteLine(File.ReadAllText(Path.Combine(Settings.InputPath, Settings.LicenseFile)));
            sw.WriteLine();
        }

        #endregion
    }
}
