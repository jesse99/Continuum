// Copyright (C) 2010 Jesse Jones
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
using System.Text.RegularExpressions;

namespace WafBuilder
{
	internal sealed class WafParser
	{
		public WafParser(string contents)
		{
			Contract.Requires(contents != null, "contents is null");
			
			DoParse(contents);
			
			for (int i = 0; i < 4; ++i)			// add some blank lines so the user can define new variables that we couldn't pull out of the waf file
				m_variables.Add(new Variable(string.Empty, string.Empty));
			
			ActiveObjects.Add(this);
		}
		
		public Process Build(string buildFile, string target, IEnumerable<Variable> vars, Dictionary<string, int> flags)
		{
			string args = "waf ";
			
			if (Array.IndexOf(ms_stdTargets, target) < 0)
				args += "build ";
				
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
			
			m_command = "python " + args + Environment.NewLine;
			
			var process = new Process();
			process.StartInfo.FileName = "python";
			process.StartInfo.Arguments = args;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.WorkingDirectory = Path.GetDirectoryName(buildFile);
			
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
		
		#region Private Methods
		private void DoParse(string contents)
		{
			// Find the targets. These will be top-level functions with a single argument
			// which are not preceded by a decoration.
			foreach (Match match in ms_targetsRe.Matches(contents))
			{
				string target = match.Groups[1].ToString();
				if (Array.IndexOf(ms_stdTargets, target) < 0)
					if (target != "options")
						m_targets.Add(target);
			}
			m_targets.AddRange(ms_stdTargets);
			
			// Find the variables. Waf uses custom command-line switches instead of 
			// environment variables so we need to look for add_option and add_option_group.
			// TODO: tools often define their own switches, but figuring out what they are
			// seems to require either parsing the waf --help output or parsing the tools in
			// the hidden waf directory (plus any tools the wscript explicitly loads).
			foreach (Match match in ms_optionsRe.Matches(contents))
			{
				string name = match.Groups[1].ToString();
				string value = match.Groups[2].ToString();
				if (value.StartsWith("\"") || value.StartsWith("'"))
					value = value.Substring(1, value.Length - 2);
				m_variables.Add(new Variable(name, value));
			}
		}
		#endregion
		
		#region Fields
		private List<string> m_targets = new List<string>();
		private List<Variable> m_variables = new List<Variable>();
		private string m_command;
		
		private static Regex ms_targetsRe = new Regex(@"(?<! @[^\r\n]+[\r\n]+) ^def \s+ (\w[_\w]*) \s* \( \s* \w[_\w]* \s* \)", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
		private static Regex ms_optionsRe = new Regex(@"^ \s+ \w[_\w]* \s* \. \s* add_option \s* \( \s* ['""] ([^'""]+) ['""] (?: .+? default \s* = \s* ([^,)\s]+))?", RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
		private string[] ms_stdTargets = new string[]{"build", "clean", "configure", "dist", "distcheck", "distclean", "install", "list", "step", "uninstall"};
		#endregion
	}
}
