// Copyright (C) 2009 Jesse Jones
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
using System.Xml;

namespace NantBuilder
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
			m_prefs = new Prefs(path);
			
			XmlDocument xml = DoReadXml(path);
			DoParseXml(xml);
			
			DoLoadPrefs();
		}
		
		public string DefaultTarget
		{
			get {return m_defaultTarget;}
		}
		
		public string[] Targets
		{
			get {return m_targets;}
		}
		
		public bool StderrIsExpected
		{
			get {return false;}
		}
		
		public string Command
		{
			get {return m_command;}
		}
		
		public Process Build(string target)
		{
			string args;
			
			if (target == "projecthelp")
			{
				args = "-projecthelp";
			}
			else
			{
				args = "-nologo " + m_prefs.GetArgs();
				
				foreach (Variable v in m_variables)
					if (v.Value.Length > 0 && v.Value != v.DefaultValue)
						args += string.Format("-D:{0}={1} ", v.Name, v.Value.Replace(" ", "\\ "));
				
				args += target;
			}
			
			m_command = "nant " + args + Environment.NewLine;
			
			Process process = new Process();
			process.StartInfo.FileName = "nant";
			process.StartInfo.Arguments = args;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.WorkingDirectory = Path.GetDirectoryName(m_path);
			
			return process;
		}
		
		public void SetBuildFlags()
		{
			var controller = new NantFlagsController(m_prefs);
			Unused.Value = NSApplication.sharedApplication().runModalForWindow(controller.window());
			controller.release();
		}
		
		public void SetBuildVariables()
		{
			Boss boss = ObjectModel.Create("DirectoryEditorPlugin");
			var vars = boss.Get<IBuildVariables>();
			if (vars.Change(m_variables))
				DoSavePrefs();
		}
		
		#region Private Methods
		private XmlDocument DoReadXml(string path)
		{
			XmlDocument xml;
			
			using (Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
			{
				using (XmlReader reader = XmlReader.Create(stream))
				{
					xml = new XmlDocument();
					xml.Load(reader);
				}
			}
			
			return xml;
		}
		
		private void DoParseXml(XmlDocument xml)
		{
			foreach (XmlNode child in xml.ChildNodes)
			{
				if (child.Name == "project")
				{
					if (child.Attributes["default"] != null)
						m_defaultTarget = child.Attributes["default"].Value;
					DoParseProject(child);
				}
			}
		}
		
		private void DoParseProject(XmlNode project)
		{
			var targets = new List<string>();
			
			foreach (XmlNode child in project.ChildNodes)
			{
				if (child.Name == "target")
				{
					if (child.Attributes["name"].Value != "*")
						targets.Add(child.Attributes["name"].Value);
				}
				else if (child.Name == "property")
				{
					if (child.Attributes["name"] != null && child.Attributes["overwrite"] != null)
						if (child.Attributes["overwrite"].Value == "false" || child.Attributes["overwrite"].Value == "0")
							if (child.Attributes["value"] != null)
								m_variables.AddIfMissing(new Variable(child.Attributes["name"].Value, child.Attributes["value"].Value));
							else
								m_variables.AddIfMissing(new Variable(child.Attributes["name"].Value, string.Empty));
				}
			}
			
			for (int i = 0; i < 4; ++i)			// add some blank lines so the user can define new variables that we couldn't pull out of the make file
				m_variables.Add(new Variable(string.Empty, string.Empty));
			
			targets.Add("projecthelp");
			
			m_targets = targets.ToArray();
		}
		
		private void DoSavePrefs()
		{
			string key = m_path + "-variables";		// this will break if the project is moved, but that should be rather rare
			
			NSMutableDictionary dict = NSMutableDictionary.Create();
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
		private string m_defaultTarget;
		private string[] m_targets = new string[0];
		private string m_command = string.Empty;
		private Prefs m_prefs;
		private List<Variable> m_variables = new List<Variable>();
		#endregion
	}
}
