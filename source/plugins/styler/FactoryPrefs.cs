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
			XmlNode xml = DoLoadXml();
			
			var globs = NSDictionary.Create();
			dict.setObject_forKey(globs, NSString.Create("language globs"));
			
			string[] languages = DoReadLanguages(xml);
			dict.setObject_forKey(NSArray.Create(languages), NSString.Create("languages"));
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
		// Note that we don't want to use IFactoryPrefs for globs because new ones
		// added Continuum need to show up.
		private void DoUpdateGlobs()
		{
			XmlNode xml = DoLoadXml();
			NSMutableDictionary globs = DoReadGlobs(xml);
			
			NSUserDefaults defaults = NSUserDefaults.standardUserDefaults();
			var user = defaults.objectForKey(NSString.Create("language globs")).To<NSDictionary>();
			globs.addEntriesFromDictionary(user);
			
			defaults.setObject_forKey(globs, NSString.Create("language globs"));
		}
		
		private XmlNode DoLoadXml()
		{
			XmlDocument xml;
			
			// Load the schema.
			string resourcesPath = NSBundle.mainBundle().resourcePath().ToString();
			string languagesPath = Path.Combine(resourcesPath, "Languages");
			string globsSchemaPath = Path.Combine(languagesPath, "Globs.schema");
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
		
		private string[] DoReadLanguages(XmlNode xml)
		{
			var languages = new List<string>();
			
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
			
			return languages.ToArray();
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
