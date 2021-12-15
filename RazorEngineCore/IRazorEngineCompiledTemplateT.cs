using System;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	public interface IRazorEngineCompiledTemplate<out T> where T : IRazorEngineTemplate
	{
		void SaveToFile(string assemblyFileName, string assemblyPDBFileName = null);

		Task<string> RunAsync(Action<T> initializer);
	}
}
