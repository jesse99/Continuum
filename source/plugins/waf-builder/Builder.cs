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

using Gear;
using Gear.Helpers;
using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace WafBuilder
{
	internal sealed class Builder : IBuilder
	{
		public void Instantiated(Boss boss)
		{	
			m_boss = boss;
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void Init(string path)
		{
			m_path = path;
			
			string contents = File.ReadAllText(path, System.Text.Encoding.UTF8);
			m_parser = new WafParser(contents);
			m_variables = new List<Variable>(m_parser.Variables);
			
			DoLoadPrefs();
		}
		
		public string DefaultTarget
		{
			get {return "build";}
		}
		
		public string[] Targets
		{
			get {return m_parser.Targets;}
		}
		
		public bool StderrIsExpected
		{
			get {return true;}		// all of the waf output goes to stderr...
		}
		
		public string Command
		{
			get {return m_parser.Command;}
		}
		
		public Process Build(string target)
		{
			return m_parser.Build(m_path, target, m_variables, m_flags);
		}
		
		public void SetBuildFlags()
		{
//			var controller = new FlagsController(m_flags);
//			Unused.Value = NSApplication.sharedApplication().runModalForWindow(controller.window());
//			controller.release();
		}
		
		public void SetBuildVariables()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var vars = boss.Get<IBuildVariables>();
			if (vars.Change(m_variables))
				DoSavePrefs();
		}
		
		#region Private Methods
		private void DoSavePrefs()
		{
			// environment variables
			string key = m_path + "-variables";		// this will break if the project is moved, but that should be rather rare
			
			var dict = NSMutableDictionary.Create();
			foreach (var entry in m_variables)
			{
				if (entry.Value.Length > 0 && entry.Value != entry.DefaultValue)
					dict.setObject_forKey(NSString.Create(entry.Value), NSString.Create(entry.Name));
			}
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			defaults.setObject_forKey(dict, NSString.Create(key));
		}
		
		private void DoLoadPrefs()
		{
			// environment variables
			string key = m_path + "-variables";
			string value;
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			NSObject pref = defaults.objectForKey(NSString.Create(key));
			if (!NSObject.IsNullOrNil(pref))
			{
				NSMutableDictionary dict = pref.To<NSMutableDictionary>();
				
				foreach (var entry in dict)
				{
					string name = entry.Key.ToString();
					value = entry.Value.ToString();
					
					int i = m_variables.FindIndex(e => e.Name == name);
					if (i >= 0)
					{
						Variable old = m_variables[i];
						m_variables[i] = new Variable(old.Name, old.DefaultValue, value);
					}
					else
					{
						Variable v = new Variable(name, string.Empty, value);
						i = m_variables.FindIndex(e => e.Name.Length == 0);
						if (i >= 0)
							m_variables.Insert(i, v);
						else
							m_variables.Add(v);
					}
				}
			}
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		private string m_path;
		private WafParser m_parser;
		
		private List<Variable> m_variables;
		private Dictionary<string, int> m_flags = new Dictionary<string, int>();
		#endregion
	}
}
