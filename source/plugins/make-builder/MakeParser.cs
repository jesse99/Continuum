// Copyright (C) 2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MakeBuilder
{
	internal sealed class MakeParser
	{
		public MakeParser(NSString contents)
		{
			Contract.Requires(!NSObject.IsNullOrNil(contents), "contents is null");
			
			m_scanner = NSScanner.scannerWithString(contents);
			m_scanner.setCharactersToBeSkipped(NSCharacterSet.whitespaceCharacterSet());	// does not include new lines
			m_scanner.setCaseSensitive(true);
			
			m_nameChars = NSMutableCharacterSet.Create();
			m_nameChars.formUnionWithCharacterSet(NSCharacterSet.alphanumericCharacterSet());		
			m_nameChars.addCharactersInString(NSString.Create("_-"));		
			
			m_eolChars = NSMutableCharacterSet.Create();
			m_eolChars.addCharactersInString(NSString.Create("\r\n#"));		// lines are ended by comments or new line characters
			
			DoParse();
			
			for (int i = 0; i < 4; ++i)			// add some blank lines so the user can define new variables that we couldn't pull out of the make file
				m_variables.Add(new Variable(string.Empty, string.Empty));
			
			ActiveObjects.Add(this);
		}
		
		public Process Build(string buildFile, string target, IEnumerable<Variable> vars, Dictionary<string, int> flags)
		{
			string args = string.Empty;
			
			foreach (KeyValuePair<string, int> f in flags)
				if (f.Value == 1)
					args += string.Format("--{0} ", f.Key);
			
			foreach (Variable v in vars)
				if (v.Value.Length > 0 && v.Value != v.DefaultValue)
					if (v.Value.IndexOf(' ') >= 0)
						args += string.Format("{0}=\"{1}\" ", v.Name, v.Value);
					else
						args += string.Format("{0}={1} ", v.Name, v.Value);
			args += target;
			
			m_command = "make " + args + Environment.NewLine;
			
			Process process = new Process();
			
			process.StartInfo.FileName               = "make";
			process.StartInfo.Arguments              = args;
			process.StartInfo.RedirectStandardError  = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute        = false;
			process.StartInfo.WorkingDirectory       = Path.GetDirectoryName(buildFile);
			
			return process;
		}
		
		public string[] Targets
		{
			get {return m_targets.ToArray();}
		}
		
		public Variable[] Variables
		{
			get {return m_variables.ToArray();}
		}
				
		// Returns the command which was used to build.
		public string Command
		{
			get {return m_command;}
		}
		
		#region Private Methods ---------------------------------------------------
		private void DoParse()
		{		
			while (!m_scanner.isAtEnd())
			{
				NSString name, token;
				if (m_scanner.scanCharactersFromSet_intoString(m_nameChars, out name))
				{
					if (name.ToString() == "ifdef" || name.ToString() == "ifndef")
					{
						Variable v = DoParseDefine();
						if (v != null)
							if (!m_variables.Any(x => x.Name == v.Name))
								m_variables.Add(v);
					}
					else if (m_scanner.scanString_intoString(NSString.Create(":"), out token))
					{
						if (!m_scanner.scanString_intoString(NSString.Create("="), out token))
							if (name.ToString() != "else")
								m_targets.Add(name.ToString());
					}
					else
					{
						Variable v = DoParseVariable(name.ToString());
						if (v != null)
						{
							Variable old = m_variables.SingleOrDefault(x => x.Name == v.Name);
							if (old != null)
								old.DefaultValue = v.DefaultValue;
							else
								m_variables.Add(v);
						}
					}
				}
				
				DoSkipToNextLine();
			}
		}
				
		// Target := Name '?=' .+
		private Variable DoParseVariable(string name)
		{
			Variable variable = null;
					
			NSString token;
			if (m_scanner.scanString_intoString(NSString.Create("?="), out token))
			{
				NSString value;
				if (m_scanner.scanUpToCharactersFromSet_intoString(m_eolChars, out value))
				{
					variable = new Variable(name, value.ToString().Trim());
				}
			}
			
			return variable;	
		}
		
		// Target := ('ifdef' | 'ifndef')  Name
		private Variable DoParseDefine()
		{
			Variable variable = null;
			
			NSString name;
			if (m_scanner.scanCharactersFromSet_intoString(m_nameChars, out name))
			{
				variable = new Variable(name.ToString(), string.Empty);
			}
			
			return variable;
		}
				
		private void DoSkipToNextLine()
		{
			NSString token;
			Unused.Value = m_scanner.scanUpToCharactersFromSet_intoString(NSCharacterSet.newlineCharacterSet(), out token);
			Unused.Value = m_scanner.scanCharactersFromSet_intoString(NSCharacterSet.newlineCharacterSet(), out token);
		}
		#endregion
			
		#region Fields ------------------------------------------------------------
		private List<string> m_targets = new List<string>();
		private List<Variable> m_variables = new List<Variable>();
		private string m_command;
		private NSScanner m_scanner;
		private NSMutableCharacterSet m_nameChars;
		private NSMutableCharacterSet m_eolChars;
		#endregion
	}
}