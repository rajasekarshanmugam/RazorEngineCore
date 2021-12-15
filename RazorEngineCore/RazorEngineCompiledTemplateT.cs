using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace RazorEngineCore
{
	/// <summary>
	/// Class RazorEngineCompiledTemplate.
	/// Implements the <see cref="RazorEngineCore.IRazorEngineCompiledTemplate{T}" />
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <seealso cref="RazorEngineCore.IRazorEngineCompiledTemplate{T}" />
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

		/// <summary>
		/// Loads from file.
		/// </summary>
		/// <param name="fileName">Name of the file.</param>
		/// <param name="pdbFileName">Name of the PDB file.</param>
		/// <returns>IRazorEngineCompiledTemplate&lt;T&gt;.</returns>
		public static IRazorEngineCompiledTemplate<T> LoadFromFile(string fileName, string pdbFileName = null)
		{
			var assemblyBytes = File.ReadAllBytes(fileName);
			var pdbBytes = pdbFileName is not null ? File.ReadAllBytes(pdbFileName) : null;
			return new RazorEngineCompiledTemplate<T>(assemblyBytes, pdbBytes, false);
		}

		/// <summary>
		/// Saves to file.
		/// </summary>
		/// <param name="assemblyFileName">Name of the assembly file.</param>
		/// <param name="assemblyPDBFileName">Name of the assembly PDB file.</param>
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

		/// <summary>
		/// Run as an asynchronous operation.
		/// </summary>
		/// <param name="initializer">The initializer.</param>
		/// <returns>A Task&lt;System.String&gt; representing the asynchronous operation.</returns>
		public async Task<string> RunAsync(Action<T> initializer)
		{
			T instance = (T)Activator.CreateInstance(this._templateType);
			initializer(instance);

			await instance.ExecuteAsync();
			return await instance.ResultAsync();
		}
	}
}
