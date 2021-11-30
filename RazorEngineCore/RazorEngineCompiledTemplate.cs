using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	public class RazorEngineCompiledTemplate : IRazorEngineCompiledTemplate
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

		public static IRazorEngineCompiledTemplate LoadFromFile(string fileName, string pdbFileName = null)
		{
			var assemblyBytes = File.ReadAllBytes(fileName);
			var pdbBytes = pdbFileName is not null ? File.ReadAllBytes(pdbFileName) : null;
			return new RazorEngineCompiledTemplate(assemblyBytes, pdbBytes, false);
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

		public string Run(object model = null)
		{
			return this.RunAsync(model).GetAwaiter().GetResult();
		}

		public async Task<string> RunAsync(object model = null)
		{
			if (model is not null && model.IsAnonymous())
			{
				model = new AnonymousTypeWrapper(model);
			}

			IRazorEngineTemplate instance = (IRazorEngineTemplate)Activator.CreateInstance(this._templateType);
			instance.Model = model;

			await instance.ExecuteAsync();

			return await instance.ResultAsync();
		}
	}
}
