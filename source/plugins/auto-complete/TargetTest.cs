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

#if TEST
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;

namespace AutoComplete
{
	[TestFixture]
	public sealed class TargetTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			AssertListener.Install();
		}
		
		private bool DoGetType(string text, string target, int offset, MockTargetDatabase database)
		{
Console.WriteLine("------------------------------------");
Console.WriteLine(text);

			var locals = new CsParser.LocalsParser();
			m_target = new Target(database, locals);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			return m_target.FindType(text, target, offset, globals);
		}
		
		private bool DoGetType(string text, string target, int offset)
		{
			var database = new MockTargetDatabase();
			return DoGetType(text, target, offset, database);
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
			bool found = DoGetType(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("MyClass", m_target.FullTypeName);
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
			bool found = DoGetType(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.FullTypeName);
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
			bool found = DoGetType(text, "value", text.IndexOf("."));
			Assert.IsFalse(found);
			
			found = DoGetType(text, "value", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
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
			bool found = DoGetType(text, "value", text.IndexOf("."));
			Assert.IsFalse(found);
			
			found = DoGetType(text, "value", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
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
			bool found = DoGetType(text, "MyClass", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.FullTypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
			
			found = DoGetType(text, "Helper", text.LastIndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.Helper", m_target.FullTypeName);
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
			bool found = DoGetType(text, "int", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			// Int32
			found = DoGetType(text, "Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			// System.Int32
			found = DoGetType(text, "System.Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			// StringBuilder
			found = DoGetType(text, "StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			// System.Text.StringBuilder
			found = DoGetType(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullTypeName);
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
			bool found = DoGetType(text, "StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsFalse(found);
			
			// System.Text.StringBuilder
			found = DoGetType(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullTypeName);
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
			xxx.
		}
	}
}
";
			bool found = DoGetType(text, "alpha", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.FullTypeName);
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
			bool found = DoGetType(text, "zeta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullTypeName);
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
			bool found = DoGetType(text, "beta", text.IndexOf("if"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int16", "00-01"},
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "zeta", text.IndexOf("if"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int16", "00-01"},
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int16", "00-01"},
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int16", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "zeta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int16", "00-01"},
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int16", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Field1()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha)
		{
			xxx.
		}
		
		private long alpha;
		private long beta;
	}
}
";
			bool found = DoGetType(text, "alpha", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Field2()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass : System.SomeBase
	{
		public void Work()
		{
			xxx.
		}
		
		private long alpha;
	}
}
";
			bool found = DoGetType(text, "alpha", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.SomeBase", "00-02"},
				},
				BaseFieldTypes = new Dictionary<string, string>
				{
					{"System.SomeBase+alpha", "int"},
					{"System.SomeBase+beta", "int"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetType(text, "beta", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.SomeBase", "00-02"},
				},
				BaseFieldTypes = new Dictionary<string, string>
				{
					{"System.SomeBase+alpha", "int"},
					{"System.SomeBase+beta", "int"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullTypeName);
			Assert.IsNull(m_target.Type);
		}
		
		private Target m_target;
	}
	// TODO: need to try base type for global, each using (and current) namespace
}
#endif	// TEST
