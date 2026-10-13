using System;
using System.Collections.Generic;

namespace CznCore.Presentation;

internal static class SkinEventHub
{
	private sealed class Subscription<TEvent>(string context, Action<TEvent> handler) : IDisposable
	{
		public string Context { get; } = context;

		public Action<TEvent> Handler { get; } = handler;

		public void Dispose()
		{
			Remove(typeof(TEvent), this);
		}
	}

	private static readonly object Sync = new object();

	private static readonly Dictionary<Type, List<object>> Subscribers = new Dictionary<Type, List<object>>();

	public static IDisposable Subscribe<TEvent>(string context, Action<TEvent> handler)
	{
		ArgumentNullException.ThrowIfNull(handler, "handler");
		if (string.IsNullOrWhiteSpace(context))
		{
			throw new ArgumentException("Subscription context is required.", "context");
		}
		Subscription<TEvent> subscription = new Subscription<TEvent>(context, handler);
		lock (Sync)
		{
			if (!Subscribers.TryGetValue(typeof(TEvent), out List<object> value))
			{
				value = new List<object>();
				Subscribers.Add(typeof(TEvent), value);
			}
			value.Add(subscription);
			return subscription;
		}
	}

	public static void Publish<TEvent>(TEvent gameEvent)
	{
		try
		{
			object[] array;
			lock (Sync)
			{
				if (!Subscribers.TryGetValue(typeof(TEvent), out List<object> value) || value.Count == 0)
				{
					return;
				}
				array = value.ToArray();
			}
			object[] array2 = array;
			foreach (object obj in array2)
			{
				Subscription<TEvent> subscription = obj as Subscription<TEvent>;
				if (subscription != null)
				{
					SkinPatchGuard.Run(subscription.Context, () =>
					{
						subscription.Handler(gameEvent);
					});
				}
			}
		}
		catch (Exception ex)
		{
			SkinLog.Warn("Event publish failed for " + typeof(TEvent).Name + ": " + SkinLog.FormatException(ex));
		}
	}

	private static void Remove(Type eventType, object subscription)
	{
		lock (Sync)
		{
			if (Subscribers.TryGetValue(eventType, out List<object> value))
			{
				value.Remove(subscription);
			}
		}
	}
}
