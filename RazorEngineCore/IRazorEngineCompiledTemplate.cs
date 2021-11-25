using System.IO;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	public interface IRazorEngineCompiledTemplate
	{
		void SaveToFile(string assemblyFileName, string assemblyPDBFileName = null);

		string Run(object model = null);

		Task<string> RunAsync(object model = null);
	}
}
