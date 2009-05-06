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

#if TEST
using NUnit.Framework;
//using Shared;
using System;
using System.Collections.Generic;

namespace AutoComplete
{
	[TestFixture]
	public sealed class ResolveNameTest
	{	
		private bool DoGetTarget(string text, string name, int offset, MockTargetDatabase database)
		{
//Console.WriteLine("------------------------------------");
//Console.WriteLine(name);
//Console.WriteLine(text);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			var locals = new CsParser.LocalsParser();
			CsMember context = AutoComplete.FindDeclaration(globals, offset) as CsMember;
			var resolver = new ResolveName(context, database, locals, text, offset, globals);
			
			m_target = resolver.Resolve(name);
			return m_target != null;
		}
		
		private bool DoGetTarget(string text, string name, int offset)
		{
			var database = new MockTargetDatabase();
			return DoGetTarget(text, name, offset, database);
		}
		
		[Test]
		public void ThisCall1()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		this.
	}

	public void Process(int alpha)
	{		
	}

	public static void StaticMethod()
	{
	}
}
";
			bool found = DoGetTarget(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
		}
		
		[Test]
		public void ThisCall2()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha)
		{
			this.
		}
	
		public void Process(int alpha)
		{		
		}
	
		public static void StaticMethod()
		{
		}
	}
}
";
			bool found = DoGetTarget(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
		}
		
		[Test]
		public void Value1()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public int this[int x]
		{
			get
			{
				value.
			}
			set
			{
				value.
			}
		}
	}
}
";
			bool found = DoGetTarget(text, "value", text.IndexOf("."));
			Assert.IsFalse(found);
			
			found = DoGetTarget(text, "value", text.LastIndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Value2()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public int Weight
		{
			get
			{
				value.
			}
			set
			{
				value.
			}
		}
	}
}
";
			bool found = DoGetTarget(text, "value", text.IndexOf("."));
			Assert.IsFalse(found);
			
			found = DoGetTarget(text, "value", text.LastIndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void LocalType()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
			MyClass. 
			Helper.
		}
	}

	internal sealed class Helper
	{
		public void Process()
		{
		}
	}
}
";
			bool found = DoGetTarget(text, "MyClass", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
			
			found = DoGetTarget(text, "Helper", text.LastIndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.Helper", m_target.TypeName);
			Assert.AreEqual("Helper", m_target.Type.Name);
		}
		
		[Test]
		public void DatabaseType1()
		{
			string text = @"
using System;
using System.Text;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
			xxx.
		}
	}
}
";
			// int
			bool found = DoGetTarget(text, "int", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			// Int32
			found = DoGetTarget(text, "Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			// System.Int32
			found = DoGetTarget(text, "System.Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			// StringBuilder
			found = DoGetTarget(text, "StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			// System.Text.StringBuilder
			found = DoGetTarget(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void DatabaseType2()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
			xxx.
		}
	}
}
";
			// StringBuilder
			bool found = DoGetTarget(text, "StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsFalse(found);
			
			// System.Text.StringBuilder
			found = DoGetTarget(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void ArgVariable()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha, float beta)
		{
			aaa.
		}
	}
}
";
			bool found = DoGetTarget(text, "alpha", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void LocalVariable1()
		{
			// Note that we don't need to try the various forms of local variables here:
			// there is a separate unit test for that.
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha, float beta)
		{
			long zeta;
			xxx.
		}
	}
}
";
			bool found = DoGetTarget(text, "zeta", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Int64",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void LocalVariable2()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha, float beta)
		{
			long zeta;
			if (alpha > 0)
			{
				short beta, zeta;
				xxx.
			}
			xxx.
		}
	}
}
";
			bool found = DoGetTarget(text, "beta", text.IndexOf("if"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int16",
					"System.Int32",
					"System.Int64",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "zeta", text.IndexOf("if"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int16",
					"System.Int32",
					"System.Int64",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int16",
					"System.Int32",
					"System.Int64",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int16", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "zeta", text.IndexOf("."), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int16",
					"System.Int32",
					"System.Int64",
					"System.Single",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int16", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		private ResolvedTarget m_target;
	}
}
#endif	// TEST
