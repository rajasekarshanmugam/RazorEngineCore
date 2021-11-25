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
		public IRazorEngineCompiledTemplate<T> Compile<T>(string scriptName, string content, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null) where T : IRazorEngineTemplate
		{
			var compilationOptionsBuilder = new RazorEngineCompilationOptionsBuilder();

			compilationOptionsBuilder.AddAssemblyReference(typeof(T).Assembly);
			compilationOptionsBuilder.Inherits(typeof(T));

			builderAction?.Invoke(compilationOptionsBuilder);

			var (assemblyBytes, pdbBytes) = this.CreateAndCompileToStream(scriptName, content, compilationOptionsBuilder.Options);

			return new RazorEngineCompiledTemplate<T>(assemblyBytes, pdbBytes, true);
		}

		/// <summary>
		/// Compiles the specified file name.
		/// </summary>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="content">The content.</param>
		/// <param name="builderAction">The builder action.</param>
		/// <returns>IRazorEngineCompiledTemplate.</returns>
		public IRazorEngineCompiledTemplate Compile(string scriptName, string content, Action<IRazorEngineCompilationOptionsBuilder> builderAction = null)
		{
			var compilationOptionsBuilder = new RazorEngineCompilationOptionsBuilder();
			compilationOptionsBuilder.Inherits(typeof(RazorEngineTemplateBase));

			builderAction?.Invoke(compilationOptionsBuilder);

			var (assemblyBytes, pdbBytes) = this.CreateAndCompileToStream(scriptName, content, compilationOptionsBuilder.Options);

			return new RazorEngineCompiledTemplate(assemblyBytes, pdbBytes, true);
		}

		/// <summary>
		/// Compile to stream.
		/// </summary>
		/// <param name="scriptName">Name of the script.</param>
		/// <param name="csSource">The cs source.</param>
		/// <param name="options">The options.</param>
		/// <returns>System.ValueTuple&lt;MemoryStream, MemoryStream&gt;.</returns>
		private (byte[] AssemblyBytes, byte[] PdbBytes) CreateAndCompileToStream(string scriptName, string csSource, RazorEngineCompilationOptions options)
		{
			csSource = this.WriteDirectives(csSource, options);

			var projectDirectory = options.ProjectDirectory ?? ".";
			var engine = RazorProjectEngine.Create(
				RazorConfiguration.Default,
				RazorProjectFileSystem.Create(projectDirectory),
				(builder) =>
				{
					builder.SetNamespace(options.TemplateNamespace);
				});

			var workingDirectory = options.WorkingDirectory ?? ".";
			var sourceName = $"{scriptName}.cs";
			var sourceCodePath = Path.Combine(workingDirectory, sourceName);

			var assemblyPath = Path.ChangeExtension(sourceCodePath, "dll");
			var symbolsPath = Path.ChangeExtension(sourceCodePath, "pdb");
			var document = RazorSourceDocument.Create(csSource, sourceName);

			var codeDocument = engine.Process(
				document,
				null,
				new List<RazorSourceDocument>(),
				new List<TagHelperDescriptor>());

			var razorCSharpDocument = codeDocument.GetCSharpDocument();

			var encoding = Encoding.UTF8;
			var code = razorCSharpDocument.GeneratedCode;
			File.WriteAllText(sourceCodePath, code);

			var buffer = encoding.GetBytes(code);
			var sourceText = SourceText.From(buffer, buffer.Length, encoding, canBeEmbedded: true);
			var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, options: options.ParseOptions, path: sourceCodePath);
			var syntaxRootNode = syntaxTree.GetRoot() as CSharpSyntaxNode;
			var encodedSyntaxTree = CSharpSyntaxTree.Create(syntaxRootNode, null, sourceCodePath, encoding);

			var compilerOptions = (options.CompilationOptions ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
				.WithOutputKind(OutputKind.DynamicallyLinkedLibrary)
				.WithOptimizationLevel(options.IsDebug ? OptimizationLevel.Debug : OptimizationLevel.Release);

			var compilation = CSharpCompilation.Create(
				sourceName,
				new[]
				{
					encodedSyntaxTree
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

			var emitOptions = new EmitOptions(
				debugInformationFormat: DebugInformationFormat.PortablePdb,
				pdbFilePath: symbolsPath);

			var embeddedTexts = new List<EmbeddedText>
			{
				EmbeddedText.FromSource(sourceCodePath, sourceText),
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
				var exception = new RazorEngineCompilationException()
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
