using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RazorEngineCore
{
	public class RazorEngine : IRazorEngine
	{
		/// <summary>
		/// Compiles the specified file name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="content">The content.</param>
		/// <param name="builderAction">The builder action.</param>
		/// <returns>IRazorEngineCompiledTemplate&lt;T&gt;.</returns>
		public IRazorEngineCompiledTemplate<T> Compile<T>(string razorSourcePath, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null) where T : IRazorEngineTemplate
		{
			var compilationOptionsBuilder = new RazorEngineCompilationOptionsBuilder();

			compilationOptionsBuilder.AddAssemblyReference(typeof(T).Assembly);
			compilationOptionsBuilder.Inherits(typeof(T));

			builderAction?.Invoke(compilationOptionsBuilder);

			var (assemblyBytes, pdbBytes) = this.CreateAndCompileToStream(razorSourcePath, compilationOptionsBuilder.Options);

			return new RazorEngineCompiledTemplate<T>(assemblyBytes, pdbBytes, true);
		}

		/// <summary>
		/// Compile to stream.
		/// </summary>
		/// <param name="razorSourcePath">The razor source path.</param>
		/// <param name="options">The options.</param>
		/// <returns>System.ValueTuple&lt;MemoryStream, MemoryStream&gt;.</returns>
		private (byte[] AssemblyBytes, byte[] PdbBytes) CreateAndCompileToStream(string razorSourcePath, RazorEngineCompilationOptions options)
		{
			var razorSource = File.ReadAllText(razorSourcePath);
			razorSource = this.WriteDirectives(razorSource, options);

			var projectDirectory = !string.IsNullOrEmpty(options.ProjectDirectory) ? options.ProjectDirectory : ".";
			var engine = RazorProjectEngine.Create(
				RazorConfiguration.Default,
				RazorProjectFileSystem.Create(projectDirectory),
				(builder) =>
				{
					builder.SetNamespace(options.TemplateNamespace);
				});

			var workingDirectory = options.WorkingDirectory ?? ".";

			var razorsourceName = Path.GetFileName(razorSourcePath);
			var cssourceName = $"{razorsourceName}.cs";
			var document = RazorSourceDocument.Create(razorSource, razorsourceName);

			var codeDocument = engine.Process(
				document,
				null,
				new List<RazorSourceDocument>(),
				new List<TagHelperDescriptor>());

			var razorCSharpDocument = codeDocument.GetCSharpDocument();

			var encoding = Encoding.UTF8;
			var cscode = razorCSharpDocument.GeneratedCode;

			var razorbuffer = encoding.GetBytes(razorSource);
			var razorsourceText = SourceText.From(razorbuffer, razorbuffer.Length, encoding, canBeEmbedded: true);

			var csbuffer = encoding.GetBytes(cscode);
			var cssourceText = SourceText.From(csbuffer, csbuffer.Length, encoding, canBeEmbedded: true);
			var cssyntaxTree = CSharpSyntaxTree.ParseText(cssourceText, options: options.ParseOptions, path: razorSource);
			var cssyntaxRootNode = cssyntaxTree.GetRoot() as CSharpSyntaxNode;
			var csencodedSyntaxTree = CSharpSyntaxTree.Create(cssyntaxRootNode, null, cssourceName, encoding);

			var compilerOptions = (options.CompilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
				.WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
				.WithOptimizationLevel(options.IsDebug ? OptimizationLevel.Debug : OptimizationLevel.Release);

			var compilation = CSharpCompilation.Create(
				cssourceName,
				new[]
				{
					csencodedSyntaxTree
				},
				options.ReferencedAssemblies
				   .Select(ass =>
				   {
#if NETSTANDARD2_0 || NET6_0_OR_GREATER
					   return MetadataReference.CreateFromFile(ass.Location);
#else
					   unsafe
					   {
						   ass.TryGetRawMetadata(out byte* blob, out int length);
						   ModuleMetadata moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
						   AssemblyMetadata assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
						   PortableExecutableReference metadataReference = assemblyMetadata.GetReference();

						   return metadataReference;
					   }
#endif
				   })
					.Concat(options.MetadataReferences)
					.ToList(),
				compilerOptions);

			var symbolsName = Path.ChangeExtension(razorsourceName, ".pdb");
			var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb, pdbFilePath: symbolsName);

			var embeddedTexts = new List<EmbeddedText>
			{
				EmbeddedText.FromSource(razorsourceName, razorsourceText),
				//EmbeddedText.FromSource(cssourceName, cssourceText),
			};

			using var assemblyStream = new MemoryStream();
			using var symbolsStream = new MemoryStream();

			var emitResult = compilation.Emit(
				peStream: assemblyStream,
				pdbStream: symbolsStream,
				embeddedTexts: embeddedTexts,
				options: emitOptions);

			if (!emitResult.Success)
			{
				var exception = new RazorEngineCompilationException
				{
					Errors = emitResult.Diagnostics.ToList(),
					GeneratedCode = razorCSharpDocument.GeneratedCode
				};

				throw exception;
			}

			return (assemblyStream.ToArray(), symbolsStream.ToArray());
		}

		private string WriteDirectives(string content, RazorEngineCompilationOptions options)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine($"@inherits {options.Inherits}");

			foreach (string entry in options.DefaultUsings)
			{
				stringBuilder.AppendLine($"@using {entry}");
			}

			stringBuilder.Append(content);

			return stringBuilder.ToString();
		}
	}
}
