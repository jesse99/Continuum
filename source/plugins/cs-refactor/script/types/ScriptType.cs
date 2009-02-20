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

using MCocoa;
using MObjc;
using Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CsRefactor.Script
{
	internal sealed class ScriptType : RefactorType
	{
		private ScriptType()
		{
			Trace.Assert(ms_instance == null, "Types should only be instantiated once");
			
			ms_instance = this;
		}
		
		public static ScriptType Instance 
		{
			get 
			{
				if (ms_instance == null)
					ms_instance = new ScriptType();
					
				return ms_instance;
			}
		}
		
		public override RefactorType Base
		{
			get {return ObjectType.Instance;}
		}
		
		public override string Name
		{
			get {return "Script";}
		}

		public override Type ManagedType
		{
			get {return typeof(Script);}
		}
		
		public void RegisterCustomMethods(Context context, Method[] methods)
		{
			RegisterAllMethods();
			
			foreach (Method method in methods)
			{
				Register(context, method);
			}
		}
		
		public void SetWriter(TextWriter writer)
		{
			Trace.Assert(writer != null, "writer is null");
			
			m_writer = writer;
		}
		
		protected override void RegisterMethods(RefactorType type)
		{
			type.Register<Script, string, object>("Ask", this.DoAsk);
			type.Register<Script>("get_HasSelection", this.DoGetHasSelection);
			type.Register<Script>("get_Globals", this.DoGetGlobals);
			type.Register<Script, string>("GetUniqueName", this.DoGetUniqueName);
			type.Register<Script, string>("Indent", this.DoIndent);
			type.Register<Script, string>("InsertAfterSelection", this.DoInsertAfterSelection);
			type.Register<Script, string>("InsertBeforeSelection", this.DoInsertBeforeSelection);
			type.Register<Script, string>("Raise", this.DoRaise);
			type.Register<Script>("get_Scope", this.DoGetScope);
			type.Register<Script, object>("Write", this.DoWrite);
			type.Register<Script, object>("WriteLine", this.DoWriteLine);
		}
		
		#region Private Methods
		private object DoAsk(Script script, string prompt, object dvalue)
		{
			object result;
			
			if (dvalue is string)
			{
				var dialog = new GetString{Title = prompt, Label = "Value:"};
				dialog.Text = (string) dvalue;
				result = dialog.Run();
				if (result == null)
					throw new OperationCanceledException();
			}
			else if (Equals(dvalue, true))
			{
				int button = Functions.NSRunInformationalAlertPanel(
					NSString.Create(prompt),	// title
					NSString.Empty,				// message, 
					NSString.Create("Yes"),		// defaultButton
					NSString.Create("No"),		// alternateButton
					null);								// otherButton
				result = button == Enums.NSOKButton;
			}
			else if (Equals(dvalue, false))
			{
				int button = Functions.NSRunInformationalAlertPanel(
					NSString.Create(prompt),	// title
					NSString.Empty,				// message, 
					NSString.Create("No"),		// defaultButton
					NSString.Create("Yes"),		// alternateButton
					null);								// otherButton
				result = button != Enums.NSOKButton;
			}
			else
				throw new InvalidOperationException("Default value must be a Boolean or String.");
				
			return result;
		}

		private object DoGetHasSelection(Script script)
		{
			return script.Context.SelLen > 0;
		}

		private object DoGetGlobals(Script script)
		{
			return script.Context.Globals;
		}

		private CsDeclaration DoFindScope(CsDeclaration declaration, int offset)
		{
			CsTypeScope outer = declaration as CsTypeScope;
			if (outer != null)
			{
				foreach (CsDeclaration nested in outer.Declarations)
				{
					if (offset >= nested.Offset && offset < nested.Offset + nested.Length)
						return DoFindScope(nested, offset);
				}
			}
			
			return declaration;
		}

		private object DoGetScope(Script script)
		{
			return DoFindScope(script.Context.Globals, script.Context.SelStart);
		}

		private object DoGetUniqueName(Script script, string inName)
		{
			CsDeclaration dec = DoFindScope(script.Context.Globals, script.Context.SelStart);

			string name = inName;
			for (int i = 2; i < 102; ++i)
			{
				if (DoIsUnique(script.Context.Text, name, dec.Offset, dec.Length))
					return name;
							
				name = inName + i;
			}
			
			throw new Exception("Couldn't find a unique name after 100 tries.");
		}
		
		private bool DoIsUnique(string text, string name, int offset, int length)
		{
			int i = offset;
			while (true)
			{
				i = text.IndexOf(name, i, length - (i - offset));
				if (i < 0)
					break;
					
				if (!DoIsNameChar(text, i - 1) && !DoIsNameChar(text, i + name.Length))
					return false;
				i += name.Length;
			}
			
			return true;
		}
		
		private bool DoIsNameChar(string text, int i)
		{
			if (i >= 0 && i < text.Length)
				return text[i] == '_' || char.IsLetterOrDigit(text[i]);
			else
				return false;
		}
		
		private object DoIndent(Script script, string tabs)
		{
			foreach (char ch in tabs)
			{
				if (ch != '\t')
					throw new InvalidOperationException("The characters passed to Indent should all be tabs");
			}
			
			return new Indent(script.Context.SelStart, script.Context.SelLen, tabs);
		}
		
		private object DoInsertAfterSelection(Script script, string text)
		{
			return new InsertAfterLine(script.Context.SelStart + script.Context.SelLen, script.Context.SelLen, text.Split('\n'));
		}
		
		private object DoInsertBeforeSelection(Script script, string text)
		{
			return new InsertBeforeLine(script.Context.SelStart, text.Split('\n'));
		}
		
		private object DoRaise(Script script, string message)
		{
			throw new ScriptAbortException(message);
		}
		
		private object DoWrite(Script script, object value)
		{
			m_writer.Write("{0}", value.Stringify());
			return null;
		}
		
		private object DoWriteLine(Script script, object value)
		{
			m_writer.WriteLine("{0}", value.Stringify());
			return null;
		}
		#endregion
		
		#region Fields
		private TextWriter m_writer = Console.Out;
		private static ScriptType ms_instance;
		#endregion
	} 
}
