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
using MCocoa;
using NUnit.Framework;
using TextEditor;
//using Shared;
using System;

[TestFixture]
public class TextMetricsTest
{
	[Test]
	public void CheckCol()
	{
		Assert.AreEqual(1, DoGetCol(string.Empty, 0));
		
		Assert.AreEqual(1, DoGetCol("foo", 0));
		Assert.AreEqual(2, DoGetCol("foo", 1));
		Assert.AreEqual(3, DoGetCol("foo", 2));
		Assert.AreEqual(4, DoGetCol("foo", 3));
		
		Assert.AreEqual(3, DoGetCol("hi\nthere", 2));
		Assert.AreEqual(1, DoGetCol("hi\nthere", 3));
		Assert.AreEqual(2, DoGetCol("hi\nthere", 4));
		
		Assert.AreEqual(2, DoGetCol("hi\rthere", 4));
	}
	
	[Test]
	public void CheckLine()
	{
		Assert.AreEqual(1, DoGetLine(string.Empty, 0));
		
		Assert.AreEqual(1, DoGetLine("foo", 0));
		Assert.AreEqual(1, DoGetLine("foo", 1));
		Assert.AreEqual(1, DoGetLine("foo", 2));
		Assert.AreEqual(1, DoGetLine("foo", 3));
		
		Assert.AreEqual(1, DoGetLine("hi\nthere", 2));
		Assert.AreEqual(2, DoGetLine("hi\nthere", 3));
		Assert.AreEqual(2, DoGetLine("hi\nthere", 4));
		Assert.AreEqual(2, DoGetLine("hi\nthere", "hi\nthere".Length));
		
		Assert.AreEqual(3, DoGetLine("hi\nthere\n", "hi\nthere\n".Length));
		
		Assert.AreEqual(2, DoGetLine("hi\rthere", 4));
	}
	
	[Test]
	public void CheckBalance()
	{
		Assert.AreEqual(new NSRange(0, 0), DoBalance("hello", 2, 0));
		
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(hey)", 0, 0));
		Assert.AreEqual(new NSRange(0, 5), DoBalance("(hey)", 1, 0));
		Assert.AreEqual(new NSRange(0, 5), DoBalance("(hey)", 2, 0));
		Assert.AreEqual(new NSRange(0, 5), DoBalance("(hey)", 3, 0));
		Assert.AreEqual(new NSRange(0, 5), DoBalance("(hey)", 4, 0));
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(hey)", 5, 0));	// balanced range doesn't intersect the original range
		
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(hey])", 1, 0));
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(hey[)", 1, 0));
		Assert.AreEqual(new NSRange(0, 7), DoBalance("(h[ey])", 1, 0));
		
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(())", 0, 0));
		Assert.AreEqual(new NSRange(0, 4), DoBalance("(())", 1, 0));
		Assert.AreEqual(new NSRange(1, 2), DoBalance("(())", 2, 0));
		Assert.AreEqual(new NSRange(0, 4), DoBalance("(())", 3, 0));
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(())", 4, 0));
		
		Assert.AreEqual(new NSRange(0, 13), DoBalance("(xx(yy) (zz))", 8, 4));
		Assert.AreEqual(new NSRange(0, 13), DoBalance("(xx(yy) (zz))", 5, 5));
		
		Assert.AreEqual(new NSRange(0, 10), DoBalance("(foo(bar))", 1,  2));
		Assert.AreEqual(new NSRange(0, 10), DoBalance("(foo(bar))", 4,  5));
		Assert.AreEqual(new NSRange(0, 10), DoBalance("(foo(bar))", 6,  4));
		
		Assert.AreEqual(new NSRange(0, 0), DoBalance("(hey[hey)", 4, 5));
		
		string text = "x(string text, NSRange range)y";
		int first = text.IndexOf("(");
		int last = text.IndexOf(")");
		Assert.AreEqual(new NSRange(first, text.Length - 2), DoBalance(text, first + 1, 0));
		Assert.AreEqual(new NSRange(first, text.Length - 2), DoBalance(text, last, 0));
	}
	
	[Test]
	public void CheckBalanceLeft()
	{
		Assert.AreEqual(-2, DoBalanceLeft("hello", 2));
		
		Assert.AreEqual(-2, DoBalanceLeft("(hey)", 1));
		Assert.AreEqual(0, DoBalanceLeft("(hey)", 4));
		Assert.AreEqual(-1, DoBalanceLeft("(hey))", 5));
		Assert.AreEqual(0, DoBalanceLeft("((hey))", 6));
		Assert.AreEqual(1, DoBalanceLeft("((hey))", 5));
		
		Assert.AreEqual(-1, DoBalanceLeft("((hey)]", 6));
		Assert.AreEqual(-1, DoBalanceLeft("([hey)]", 6));
		Assert.AreEqual(0, DoBalanceLeft("[(hey)]", 6));
		
		Assert.AreEqual(0, DoBalanceLeft("[]", 1));
	}
	
	#region Private Methods
	private int DoBalanceLeft(string text, int index)
	{
		TextMetrics metrics = new TextMetrics(text);
		return metrics.BalanceLeft(text, index);
	}
		
	private NSRange DoBalance(string text, int index, int len)
	{
		TextMetrics metrics = new TextMetrics(text);
		return metrics.Balance(text, new NSRange(index, len));
	}
		
	private int DoGetCol(string text, int index)
	{
		TextMetrics metrics = new TextMetrics(text);
		return metrics.GetCol(index);
	}
		
	private int DoGetLine(string text, int index)
	{
		TextMetrics metrics = new TextMetrics(text);
		return metrics.GetLine(index);
	}
	#endregion
}
#endif	// TEST
