using System;
using System.IO;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	public interface IRazorEngineCompiledTemplate<out T> where T : IRazorEngineTemplate
	{
		void SaveToFile(string assemblyFileName, string assemblyPDBFileName = null);

		string Run(Action<T> initializer);

		Task<string> RunAsync(Action<T> initializer);
	}
}
