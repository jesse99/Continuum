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
using Shared;
using System;
using System.Collections.Generic;

namespace AutoComplete
{
	[TestFixture]
	public sealed class ResolveTypeTest
	{
		private bool DoGetType(string text, string target, MockTargetDatabase database)
		{
//Console.WriteLine("------------------------------------");
//Console.WriteLine(text);
			
			var resolver = new ResolveType(database);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			m_target = resolver.Resolve(target, globals, true, false);
			return m_target != null;
		}
		
		private bool DoGetType(string text, string target)
		{
			var database = new MockTargetDatabase();
			return DoGetType(text, target, database);
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
			bool found = DoGetType(text, "MyClass");
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
			
			found = DoGetType(text, "Helper");
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
		}
	}
}
";
			// int
			bool found = DoGetType(text, "int", new MockTargetDatabase
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
			found = DoGetType(text, "Int32", new MockTargetDatabase
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
			found = DoGetType(text, "System.Int32", new MockTargetDatabase
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
			found = DoGetType(text, "StringBuilder", new MockTargetDatabase
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
			found = DoGetType(text, "System.Text.StringBuilder", new MockTargetDatabase
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
		}
	}
}
";
			// StringBuilder
			bool found = DoGetType(text, "StringBuilder", new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Text.StringBuilder",
				}
			});
			Assert.IsFalse(found);
			
			// System.Text.StringBuilder
			found = DoGetType(text, "System.Text.StringBuilder", new MockTargetDatabase
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
		public void Array()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
		}
	}
}
";
			bool found = DoGetType(text, "int[]", new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Array",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("array-type", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void NestedType()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
		}

		private sealed class Helper
		{
			public void Process()
			{
			}
		}
	}
}
";
			bool found = DoGetType(text, "MyClass");
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
			
			found = DoGetType(text, "Helper");
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass/Helper", m_target.TypeName);
			Assert.AreEqual("Helper", m_target.Type.Name);
		}
		
		[Test]
		public void Enums()
		{
			string text = @"
using System;

internal enum Colors
{
	Red,
	Green,
	Blue,
}

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work()
		{
		}
	}
}
";
			bool found = DoGetType(text, "Colors", new MockTargetDatabase
			{
				Types = new List<string>
				{
					"Patterns",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("Colors", m_target.TypeName);
			Assert.AreEqual("Colors", m_target.Type.Name);
			
			found = DoGetType(text, "Patterns", new MockTargetDatabase
			{
				Types = new List<string>
				{
					"Patterns",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("Patterns", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Delegates()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal delegate void Callback(int data);

	internal sealed class MyClass
	{
		public void Work()
		{
		}
	}
}
";
			bool found = DoGetType(text, "Callback", new MockTargetDatabase
			{
				Types = new List<string>
				{
					"Patterns",
					"System.Delegate",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Delegate", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		private ResolvedTarget m_target;
	}
}
#endif	// TEST
