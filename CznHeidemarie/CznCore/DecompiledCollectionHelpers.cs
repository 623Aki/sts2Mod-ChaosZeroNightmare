using System;
using System.Collections;
using System.Collections.Generic;

// GDRE 反编译时，把 C# 编译器为集合初始化生成的辅助类型（__ReadOnlyArray /
// __ReadOnlySingleElementList）的**引用**保留了下来，但这两个类型本身定义在
// 每个程序集私有的 <PrivateImplementationDetails> 里，没有被导出，
// 于是整个 CznCore 报 68 处 “未能找到类型或命名空间名”。
//
// 这里按反编译代码里的实际用法补一份等价实现。它们只在全局命名空间被引用
// （反编译产物写的是 global::_003C_003Ez__ReadOnlyArray<T>），所以放在全局命名空间。
//
// 类型名里的 _003C / _003E 是 '<' '>' 的转义写法，与编译器生成的名字一致。

/// <summary>只读数组视图，等价于编译器为集合表达式生成的 __ReadOnlyArray。</summary>
internal sealed class _003C_003Ez__ReadOnlyArray<T> : IReadOnlyList<T>
{
	private readonly T[] _items;

	public _003C_003Ez__ReadOnlyArray(T[] items)
	{
		_items = items ?? throw new ArgumentNullException(nameof(items));
	}

	public T this[int index] => _items[index];

	public int Count => _items.Length;

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}

/// <summary>单元素只读列表，等价于编译器为集合表达式生成的 __ReadOnlySingleElementList。</summary>
internal sealed class _003C_003Ez__ReadOnlySingleElementList<T> : IReadOnlyList<T>
{
	private readonly T _item;

	public _003C_003Ez__ReadOnlySingleElementList(T item)
	{
		_item = item;
	}

	public T this[int index] => index == 0
		? _item
		: throw new IndexOutOfRangeException($"索引 {index} 越界，本列表只有一个元素。");

	public int Count => 1;

	public IEnumerator<T> GetEnumerator()
	{
		yield return _item;
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
