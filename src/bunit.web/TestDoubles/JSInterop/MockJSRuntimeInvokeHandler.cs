using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Bunit.TestDoubles
{
	/// <summary>
	/// Represents an invoke handler for a mock of a <see cref="IJSRuntime"/>.
	/// </summary>
	public class MockJSRuntimeInvokeHandler
	{
		private readonly Dictionary<string, List<JSRuntimeInvocation>> _invocations = new Dictionary<string, List<JSRuntimeInvocation>>();
		private readonly Dictionary<string, List<IJSInProcessRuntimeInvocation>> _inProcessInvocations = new Dictionary<string, List<IJSInProcessRuntimeInvocation>>();
		private readonly Dictionary<string, List<object>> _plannedInvocations = new Dictionary<string, List<object>>();
		private readonly Dictionary<Type, object> _catchAllInvocations = new Dictionary<Type, object>();

		/// <summary>
		/// Gets a dictionary of all <see cref="List{JSRuntimeInvocation}"/> this mock has observed.
		/// </summary>
		public IReadOnlyDictionary<string, List<JSRuntimeInvocation>> Invocations => _invocations;

		/// <summary>
		/// Gets a dictionary of all <see cref="List{IJSInProcessRuntimeInvocation}"/> this mock has observed.
		/// </summary>
		public IReadOnlyDictionary<string, List<IJSInProcessRuntimeInvocation>> InProcessInvocations => _inProcessInvocations;

		/// <summary>
		/// Gets whether the mock is running in <see cref="JSRuntimeMockMode.Loose"/> or
		/// <see cref="JSRuntimeMockMode.Strict"/>.
		/// </summary>
		public JSRuntimeMockMode Mode { get; }

		/// <summary>
		/// Creates a <see cref="MockJSRuntimeInvokeHandler"/>.
		/// </summary>
		/// <param name="mode">The <see cref="JSRuntimeMockMode"/> the handler should use.</param>
		public MockJSRuntimeInvokeHandler(JSRuntimeMockMode mode = JSRuntimeMockMode.Loose)
		{
			Mode = mode;
		}

		/// <summary>
		/// Gets the mocked <see cref="IJSRuntime"/> instance.
		/// </summary>
		/// <returns></returns>
		public IJSRuntime ToJSRuntime()
		{
			return new MockJSRuntime(this);
		}

		/// <summary>
		/// Configure a catch all JSInterop invocation for a specific return type.
		/// </summary>
		/// <typeparam name="TResult">The result type of the invocation</typeparam>
		/// <returns>A <see cref="JSRuntimeCatchAllPlannedInvocation{TResult}"/>.</returns>
		public JSRuntimeCatchAllPlannedInvocation<TResult> Setup<TResult>()
		{
			var result = new JSRuntimeCatchAllPlannedInvocation<TResult>();

			_catchAllInvocations[typeof(TResult)] = result;

			return result;
		}

		/// <summary>
		/// Configure a planned IJSInProcess invocation with the <paramref name="identifier"/>, <paramref name="result"/> from method, and arguments
		/// passing the <paramref name="argumentsMatcher"/> test.
		/// </summary>
		/// <typeparam name="TResult">The result type of the invocation</typeparam>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="result">Result from method.</param>
		/// <param name="argumentsMatcher">A matcher that is passed arguments received in invocations to <paramref name="identifier"/>. If it returns true the invocation is matched.</param>
		/// <returns>A <see cref="IJSInProcessRuntimePlannedInvocation{TResult}"/>.</returns>
		public IJSInProcessRuntimePlannedInvocation<TResult> SetupInProcess<TResult>(string identifier, TResult result, Func<IReadOnlyList<object?>, bool> argumentsMatcher)
		{
			var invocation = new IJSInProcessRuntimePlannedInvocation<TResult>(identifier, result, argumentsMatcher);

			AddPlannedInvocation(invocation);

			return invocation;
		}

		/// <summary>
		/// Configure a planned IJSInProcess invocation with the <paramref name="identifier"/>,  <paramref name="result"/> from method, and <paramref name="arguments"/>.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="arguments">The arguments that an invocation to <paramref name="identifier"/> should match.</param>
		/// <param name="result">Result from method.</param>
		/// <returns>A <see cref="IJSInProcessRuntimePlannedInvocation{TResult}"/>.</returns>
		public IJSInProcessRuntimePlannedInvocation<TResult> SetupInProcess<TResult>(string identifier, TResult result, params object[] arguments)
		{
			return SetupInProcess<TResult>(identifier, result, args => args.SequenceEqual(arguments));
		}

		/// <summary>
		/// Configure a planned JSInterop invocation with the <paramref name="identifier"/> and arguments
		/// passing the <paramref name="argumentsMatcher"/> test.
		/// </summary>
		/// <typeparam name="TResult">The result type of the invocation</typeparam>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="argumentsMatcher">A matcher that is passed arguments received in invocations to <paramref name="identifier"/>. If it returns true the invocation is matched.</param>
		/// <returns>A <see cref="JSRuntimePlannedInvocation{TResult}"/>.</returns>
		public JSRuntimePlannedInvocation<TResult> Setup<TResult>(string identifier, Func<IReadOnlyList<object?>, bool> argumentsMatcher)
		{
			var result = new JSRuntimePlannedInvocation<TResult>(identifier, argumentsMatcher);

			AddPlannedInvocation(result);

			return result;
		}

		/// <summary>
		/// Configure a planned JSInterop invocation with the <paramref name="identifier"/> and <paramref name="arguments"/>.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="arguments">The arguments that an invocation to <paramref name="identifier"/> should match.</param>
		/// <returns>A <see cref="JSRuntimePlannedInvocation{TResult}"/>.</returns>
		public JSRuntimePlannedInvocation<TResult> Setup<TResult>(string identifier, params object[] arguments)
		{
			return Setup<TResult>(identifier, args => args.SequenceEqual(arguments));
		}

		/// <summary>
		/// Configure a planned JSInterop invocation with the <paramref name="identifier"/> and arguments
		/// passing the <paramref name="argumentsMatcher"/> test, that should not receive any result.
		/// </summary>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="argumentsMatcher">A matcher that is passed arguments received in invocations to <paramref name="identifier"/>. If it returns true the invocation is matched.</param>
		/// <returns>A <see cref="JSRuntimePlannedInvocation"/>.</returns>
		public JSRuntimePlannedInvocation SetupVoid(string identifier, Func<IReadOnlyList<object?>, bool> argumentsMatcher)
		{
			var result = new JSRuntimePlannedInvocation(identifier, argumentsMatcher);

			AddPlannedInvocation(result);

			return result;
		}

		/// <summary>
		/// Configure a planned JSInterop invocation with the <paramref name="identifier"/>
		/// and <paramref name="arguments"/>, that should not receive any result.
		/// </summary>
		/// <param name="identifier">The identifier to setup a response for</param>
		/// <param name="arguments">The arguments that an invocation to <paramref name="identifier"/> should match.</param>
		/// <returns>A <see cref="JSRuntimePlannedInvocation"/>.</returns>
		public JSRuntimePlannedInvocation SetupVoid(string identifier, params object[] arguments)
		{
			return SetupVoid(identifier, args => args.SequenceEqual(arguments));
		}

		/// <summary>
		/// Configure a catch all JSInterop invocation, that should not receive any result.
		/// </summary>
		/// <returns>A <see cref="JSRuntimeCatchAllPlannedInvocation"/>.</returns>
		public JSRuntimeCatchAllPlannedInvocation SetupVoid()
		{
			var result = new JSRuntimeCatchAllPlannedInvocation();

			_catchAllInvocations[typeof(object)] = result;

			return result;
		}

		private void AddPlannedInvocation<TResult>(JSRuntimePlannedInvocation<TResult> planned)
		{
			if (!_plannedInvocations.ContainsKey(planned.Identifier))
			{
				_plannedInvocations.Add(planned.Identifier, new List<object>());
			}
			_plannedInvocations[planned.Identifier].Add(planned);
		}

		private void AddPlannedInvocation<TResult>(IJSInProcessRuntimePlannedInvocation<TResult> planned)
		{
			if (!_plannedInvocations.ContainsKey(planned.Identifier))
			{
				_plannedInvocations.Add(planned.Identifier, new List<object>());
			}
			_plannedInvocations[planned.Identifier].Add(planned);
		}

		private void AddInvocation(JSRuntimeInvocation invocation)
		{
			if (!_invocations.ContainsKey(invocation.Identifier))
			{
				_invocations.Add(invocation.Identifier, new List<JSRuntimeInvocation>());
			}
			_invocations[invocation.Identifier].Add(invocation);
		}

		private void AddInvocation(IJSInProcessRuntimeInvocation invocation)
		{
			if (!_inProcessInvocations.ContainsKey(invocation.Identifier))
			{
				_inProcessInvocations.Add(invocation.Identifier, new List<IJSInProcessRuntimeInvocation>());
			}
			_inProcessInvocations[invocation.Identifier].Add(invocation);
		}

#if NET5_0
		private class MockJSRuntime : IJSRuntime, IJSInProcessRuntime, IJSUnmarshalledRuntime
#else
		private class MockJSRuntime : IJSRuntime, IJSInProcessRuntime
#endif
		{
			private readonly MockJSRuntimeInvokeHandler _handlers;

			public MockJSRuntime(MockJSRuntimeInvokeHandler mockJSRuntimeInvokeHandler)
			{
				_handlers = mockJSRuntimeInvokeHandler;
			}

			public TValue Invoke<TValue>(string identifier, params object?[]? args)
			{
				var invocation = new IJSInProcessRuntimeInvocation(identifier, args);
				_handlers.AddInvocation(invocation);

				return TryHandlePlannedInvocation<TValue>(identifier, invocation)
					?? default!;
			}

			public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
				=> InvokeAsync<TValue>(identifier, default, args);

			public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
			{
				var invocation = new JSRuntimeInvocation(identifier, cancellationToken, args);
				_handlers.AddInvocation(invocation);

				return TryHandlePlannedInvocationAsync<TValue>(identifier, invocation)
					?? new ValueTask<TValue>(default(TValue)!);
			}

			private ValueTask<TValue>? TryHandlePlannedInvocationAsync<TValue>(string identifier, JSRuntimeInvocation invocation)
			{
				ValueTask<TValue>? result = default;
				if (_handlers._plannedInvocations.TryGetValue(identifier, out var plannedInvocations))
				{
					var planned = plannedInvocations.OfType<JSRuntimePlannedInvocationBase<TValue>>()
						.SingleOrDefault(x => x.Matches(invocation));

					if (planned is not null)
					{
						var task = planned.RegisterInvocation(invocation);
						return new ValueTask<TValue>(task);
					}
				}

				if (_handlers._catchAllInvocations.TryGetValue(typeof(TValue), out var catchAllInvocation))
				{
					var planned = catchAllInvocation as JSRuntimePlannedInvocationBase<TValue>;

					if (planned is not null)
					{
						var task = ((JSRuntimePlannedInvocationBase<TValue>)catchAllInvocation).RegisterInvocation(invocation);
						return new ValueTask<TValue>(task);
					}
				}

				if (result is null && _handlers.Mode == JSRuntimeMockMode.Strict)
				{
					throw new UnplannedJSInvocationException(invocation);
				}

				return result;
			}

			private TValue? TryHandlePlannedInvocation<TValue>(string identifier, IJSInProcessRuntimeInvocation invocation)
			{
				TValue? result = default;
				if (_handlers._plannedInvocations.TryGetValue(identifier, out var plannedInvocations))
				{
					var planned = plannedInvocations.OfType<IJSInProcessRuntimePlannedInvocation<TValue>>()
						.SingleOrDefault(x => x.Matches(invocation));

					if (planned is not null)
					{
						return planned.Result;
					}
				}

				if (_handlers._catchAllInvocations.TryGetValue(typeof(TValue), out var catchAllInvocation))
				{
					var planned = catchAllInvocation as IJSInProcessRuntimePlannedInvocation<TValue>;

					if (planned is not null)
					{
						return planned.Result;
					}
				}

				if (result is null && _handlers.Mode == JSRuntimeMockMode.Strict)
				{
					throw new UnplannedIJSInProcessInvocationException(invocation);
				}

				return result;
			}

#if NET5_0
			public TResult InvokeUnmarshalled<TResult>(string identifier)
			{
				return Invoke<TResult>(identifier);
			}

			public TResult InvokeUnmarshalled<T0, TResult>(string identifier, T0 arg0)
			{
				return Invoke<TResult>(identifier, arg0);
			}

			public TResult InvokeUnmarshalled<T0, T1, TResult>(string identifier, T0 arg0, T1 arg1)
			{
				return Invoke<TResult>(identifier, arg0, arg1);
			}

			public TResult InvokeUnmarshalled<T0, T1, T2, TResult>(string identifier, T0 arg0, T1 arg1, T2 arg2)
			{
				return Invoke<TResult>(identifier, arg0, arg1);
			}
#endif
		}
	}
}
