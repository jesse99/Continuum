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
using System.Xml.Schema;

namespace Styler
{
	internal sealed class FactoryPrefs : IFactoryPrefs, IObserver
	{
		public void Instantiated(Boss boss)
		{
			m_boss = boss;
			Broadcaster.Register("starting event loop", this);
		}
		
		public Boss Boss
		{
			get {return m_boss;}
		}
		
		public void OnInitFactoryPref(NSMutableDictionary dict)
		{
			DoCreateDirectories();
			DoCopyMissingFiles();
			DoOverwriteFiles();
			
			var globs = NSDictionary.Create();
			dict.setObject_forKey(globs, NSString.Create("language globs"));
			
			List<string> languages = new List<string>();
			XmlNode xml = DoLoadXml("standard");
			DoReadLanguages(xml, languages);
			
			xml = DoLoadXml("user");
			DoReadLanguages(xml, languages);
			
			dict.setObject_forKey(NSArray.Create(languages.ToArray()), NSString.Create("languages"));
		}
		
		public void OnBroadcast(string name, object value)
		{
			switch (name)
			{
				case "starting event loop":
					DoUpdateGlobs();
					break;
					
				default:
					Contract.Assert(false, "bad name: " + name);
					break;
			}
		}
		
		#region Private Methods
		private void DoCreateDirectories()
		{
			string installedPath = Path.Combine(Paths.ScriptsPath, "languages");
			if (!Directory.Exists(installedPath))
				Directory.CreateDirectory(installedPath);
			
			string path = Path.Combine(installedPath, "standard");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
			
			path = Path.Combine(installedPath, "user");
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}
		
		private void DoCopyMissingFiles()
		{
			string installedPath = Path.Combine(Paths.ScriptsPath, "languages");
			string path = NSBundle.mainBundle().resourcePath().description();
			string resourcesPath = Path.Combine(path, "languages");
			
			string standardPath = Path.Combine(installedPath, "standard");
			string[] resourceFiles = Directory.GetFiles(resourcesPath);
			
			var scripts = new List<string>();
			scripts.AddRange(Directory.GetFiles(standardPath));
			
			try
			{
				foreach (string src in resourceFiles)
				{
					string name = Path.GetFileName(src);
					if (name[0] != '.')
					{
						if (!scripts.Exists(s => Path.GetFileName(s) == name))
						{
							string dst = Path.Combine(standardPath, name);
							File.Copy(src, dst);
						}
					}
				}
				
				string globsFile = Path.Combine(installedPath, "user/Globs.xml");
				if (!File.Exists(globsFile))
				{
					string contents = @"<!-- See ../standard/README for help on adding a custom language. -->
<!-- This file overrides the globs defined in ../standard/Globs.xml. -->
<!-- The Language Globs preferences panel overrides globs defined in this file. -->
<Globs>
</Globs>
";
					File.WriteAllText(globsFile, contents);
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "Couldn't copy files to '{0}'.", standardPath);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
		}
		
		private void DoOverwriteFiles()
		{
			string installedPath = Path.Combine(Paths.ScriptsPath, "languages");
			string standardPath = Path.Combine(installedPath, "standard");
			
			string path = NSBundle.mainBundle().resourcePath().description();
			string resourcesPath = Path.Combine(path, "languages");
			string[] resourceScripts = Directory.GetFiles(resourcesPath);
			
			try
			{
				foreach (string src in resourceScripts)
				{
					string name = Path.GetFileName(src);
					string dst = Path.Combine(standardPath, name);
					
					if (name[0] != '.')
					{
						if (File.Exists(dst))
						{
							if (File.GetLastWriteTime(src) > File.GetLastWriteTime(dst))
							{
								File.Delete(dst);
								File.Copy(src, dst);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.WriteLine(TraceLevel.Warning, "Errors", "Couldn't update files in '{0}'.", installedPath);
				Log.WriteLine(TraceLevel.Warning, "Errors", e.Message);
			}
		}
		
		// Note that we don't want to use IFactoryPrefs for globs because new ones
		// added by Continuum need to show up.
		private void DoUpdateGlobs()
		{
			XmlNode xml = DoLoadXml("standard");
			NSMutableDictionary globs = DoReadGlobs(xml);
			
			xml = DoLoadXml("user");
			NSMutableDictionary globs2 = DoReadGlobs(xml);
			globs.addEntriesFromDictionary(globs2);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var prefs = defaults.objectForKey(NSString.Create("language globs")).To<NSDictionary>();
			globs.addEntriesFromDictionary(prefs);
			
			defaults.setObject_forKey(globs, NSString.Create("language globs"));
		}
		
		private XmlNode DoLoadXml(string directory)
		{
			XmlDocument xml;
			
			// Load the schema.
			string installedPath = Path.Combine(Paths.ScriptsPath, "languages");
			string standardPath = Path.Combine(installedPath, "standard");
			string globsSchemaPath = Path.Combine(standardPath, "Globs.schema");
			using (Stream stream = new FileStream(globsSchemaPath, FileMode.Open, FileAccess.Read))
			{
				XmlSchema schema = XmlSchema.Read(stream, this.DoValidationEvent);
				
				// Setup the xml parsing options.
				XmlReaderSettings settings = new XmlReaderSettings();
				settings.ValidationEventHandler += this.DoValidationEvent;
				settings.ValidationType = ValidationType.Schema;
				settings.IgnoreComments = true;
				settings.Schemas.Add(schema);
				
				// Load the xml file.
				string languagesPath = Path.Combine(installedPath, directory);
				string globsPath = Path.Combine(languagesPath, "Globs.xml");
				using (Stream stream2 = new FileStream(globsPath, FileMode.Open, FileAccess.Read))
				{
					using (XmlReader reader = XmlReader.Create(stream2, settings))
					{
						xml = new XmlDocument();
						xml.Load(reader);
					}
				}
			}
			
			return xml;
		}
		
		private NSMutableDictionary DoReadGlobs(XmlNode xml)
		{
			var dict = NSMutableDictionary.Create();
			
			foreach (XmlNode child in xml.ChildNodes)
			{
				if (child.Name == "Globs")
				{
					foreach (XmlNode grandchild in child.ChildNodes)
					{
						if (grandchild.Name == "Glob")
						{
							string language = grandchild.Attributes["language"].Value;
							string[] globs = Glob.Split(grandchild.InnerText);
							
							foreach (string glob in globs)
							{
								NSString key = NSString.Create(glob);
								if (NSObject.IsNullOrNil(dict.objectForKey(key)))
									dict.setObject_forKey(NSString.Create(language), key);
								else
									Console.Error.WriteLine("glob '{0}' was declared twice.", glob);
							}
						}
					}
				}
			}
			
			return dict;
		}
		
		private void DoReadLanguages(XmlNode xml, List<string> languages)
		{
			foreach (XmlNode child in xml.ChildNodes)
			{
				if (child.Name == "Globs")
				{
					foreach (XmlNode grandchild in child.ChildNodes)
					{
						if (grandchild.Name == "Glob")
						{
							string language = grandchild.Attributes["language"].Value;
							if (!languages.Contains(language))
								languages.Add(language);
						}
					}
				}
			}
		}
		
		private void DoValidationEvent(object sender, ValidationEventArgs e)
		{
			if (e.Severity == XmlSeverityType.Warning)
				Console.WriteLine("{0}", e.Message);
			else
				throw e.Exception;
		}
		#endregion
		
		#region Fields
		private Boss m_boss;
		#endregion
	}
}
