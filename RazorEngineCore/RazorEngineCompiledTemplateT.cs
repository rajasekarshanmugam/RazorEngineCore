using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	public class RazorEngineCompiledTemplate<T> : IRazorEngineCompiledTemplate<T> where T : IRazorEngineTemplate
	{
		private readonly byte[] _assemblyBytes;
		private readonly byte[] _pdbBytes;
		private readonly Type _templateType;

		/// <summary>
		/// Initializes a new instance of the <see cref="RazorEngineCompiledTemplate"/> class.
		/// </summary>
		/// <param name="assemblyBytes">The assembly bytes.</param>
		/// <param name="pdbBytes">The PDB bytes.</param>
		/// <param name="cache">if set to <c>true</c> [cache].</param>
		internal RazorEngineCompiledTemplate(byte[] assemblyBytes, byte[] pdbBytes, bool cache)
		{
			if (cache)
			{
				this._assemblyBytes = assemblyBytes;
				this._pdbBytes = pdbBytes;
			}

			using var assemblyStream = new MemoryStream(assemblyBytes);
			using var pdbStream = new MemoryStream(assemblyBytes);
			var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream, pdbStream);
			this._templateType = assembly.GetType("TemplateNamespace.Template");
		}

		public static IRazorEngineCompiledTemplate<T> LoadFromFile(string fileName, string pdbFileName = null)
		{
			var assemblyBytes = File.ReadAllBytes(fileName);
			var pdbBytes = pdbFileName is not null ? File.ReadAllBytes(pdbFileName) : null;
			return new RazorEngineCompiledTemplate<T>(assemblyBytes, pdbBytes, false);
		}

		public void SaveToFile(string assemblyFileName, string assemblyPDBFileName = null)
		{
			if (this._assemblyBytes is not null)
			{
				File.WriteAllBytes(assemblyFileName, this._assemblyBytes);
			}
			if (this._pdbBytes is not null)
			{
				File.WriteAllBytes(assemblyPDBFileName, this._pdbBytes);
			}
		}

		public string Run(Action<T> initializer)
		{
			return this.RunAsync(initializer).GetAwaiter().GetResult();
		}

		public async Task<string> RunAsync(Action<T> initializer)
		{
			T instance = (T)Activator.CreateInstance(this._templateType);
			initializer(instance);

			await instance.ExecuteAsync();
			return await instance.ResultAsync();
		}
	}
}
