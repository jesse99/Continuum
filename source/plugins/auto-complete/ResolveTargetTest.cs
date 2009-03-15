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
	public sealed class ResolveTargetTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			AssertListener.Install();
		}
		
		private bool DoGetTarget(string text, string target, int offset, MockTargetDatabase database)
		{
//Console.WriteLine("------------------------------------");
//Console.WriteLine(target);
//Console.WriteLine(text);
			
			var locals = new CsParser.LocalsParser();
			var resolver = new ResolveTarget(database, locals);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			m_target = resolver.Resolve(text, target, offset, globals).First;
			return m_target != null;
		}
		
		private bool DoGetTarget(string text, string target, int offset)
		{
			var database = new MockTargetDatabase();
			return DoGetTarget(text, target, offset, database);
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
			Assert.AreEqual("MyClass", m_target.FullName);
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
			Assert.AreEqual("CoolLib.MyClass", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
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
			Assert.AreEqual("CoolLib.MyClass", m_target.FullName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
			
			found = DoGetTarget(text, "Helper", text.LastIndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.Helper", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			// Int32
			found = DoGetTarget(text, "Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			// System.Int32
			found = DoGetTarget(text, "System.Int32", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			// StringBuilder
			found = DoGetTarget(text, "StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			// System.Text.StringBuilder
			found = DoGetTarget(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsFalse(found);
			
			// System.Text.StringBuilder
			found = DoGetTarget(text, "System.Text.StringBuilder", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Text.StringBuilder", "00-02"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Text.StringBuilder", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullName);
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
				Hashes = new Dictionary<string, string>
				{
					{"System.Int16", "00-01"},
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.Single", "00-01"}
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Single", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "zeta", text.IndexOf("if"), new MockTargetDatabase
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
			Assert.AreEqual("System.Int64", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.IndexOf("."), new MockTargetDatabase
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
			Assert.AreEqual("System.Int16", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "zeta", text.IndexOf("."), new MockTargetDatabase
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
			Assert.AreEqual("System.Int16", m_target.FullName);
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
			bool found = DoGetTarget(text, "alpha", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullName);
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
			bool found = DoGetTarget(text, "alpha", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.SomeBase", "00-02"},
				},
				BaseFieldTypes = new Dictionary<string, string>
				{
					{"System.SomeBase+alpha", "System.Int32"},
					{"System.SomeBase+beta", "System.Int32"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, "beta", text.LastIndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
					{"System.SomeBase", "00-02"},
				},
				BaseClasses = new Dictionary<string, string>
				{
					{"CoolLib.MyClass", "System.SomeBase"},
				},
				BaseFieldTypes = new Dictionary<string, string>
				{
					{"System.SomeBase+alpha", "System.Int32"},
					{"System.SomeBase+beta", "System.Int32"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Property()
		{
			string text = @"
using System;

namespace CoolLib
{
	internal sealed class MyClass
	{
		public int Alpha {get; set;}
		
		public void Work(int alpha, float beta)
		{
			ppp.
		}
	}
}
";
			bool found = DoGetTarget(text, "Alpha", text.IndexOf("."), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		private ResolvedTarget m_target;
	}
}
#endif	// TEST
