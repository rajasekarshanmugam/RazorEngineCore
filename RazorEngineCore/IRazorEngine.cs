using System;

namespace RazorEngineCore
{
	public interface IRazorEngine
	{
		/// <summary>
		/// Compiles the specified file name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="content">The content.</param>
		/// <param name="builderAction">The builder action.</param>
		/// <returns>IRazorEngineCompiledTemplate&lt;T&gt;.</returns>
		IRazorEngineCompiledTemplate<T> Compile<T>(string scriptName, string content, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null)
			where T : IRazorEngineTemplate;

		/// <summary>
		/// Compiles the specified file name.
		/// </summary>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="content">The content.</param>
		/// <param name="builderAction">The builder action.</param>
		/// <returns>IRazorEngineCompiledTemplate.</returns>
		IRazorEngineCompiledTemplate Compile(string scriptName, string content, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null);
	}
}
