using System;

namespace RazorEngineCore
{
	public interface IRazorEngine
	{
		/// <summary>
		/// Compiles the specified file name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="razorSourcePath">The razor source path.</param>
		/// <param name="builderAction">The builder action.</param>
		/// <returns>IRazorEngineCompiledTemplate&lt;T&gt;.</returns>
		IRazorEngineCompiledTemplate<T> Compile<T>(string razorSourcePath, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null)
			where T : IRazorEngineTemplate;
	}
}
