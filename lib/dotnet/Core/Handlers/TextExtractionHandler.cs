﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.SemanticMemory.Core.AppBuilders;
using Microsoft.SemanticKernel.SemanticMemory.Core.Pipeline;
using Microsoft.SemanticKernel.Services.DataFormats.Office;
using Microsoft.SemanticKernel.Services.DataFormats.Pdf;

namespace Microsoft.SemanticKernel.SemanticMemory.Core.Handlers;

/// <summary>
/// Memory ingestion pipeline handler responsible for extracting text from files and saving it to content storage.
/// </summary>
public class TextExtractionHandler : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;
    private readonly ILogger<TextExtractionHandler> _log;

    /// <summary>
    /// Note: stepName and other params are injected with DI, <see cref="DependencyInjection.UseHandler{THandler}"/>
    /// </summary>
    /// <param name="stepName">Pipeline step for which the handler will be invoked</param>
    /// <param name="orchestrator">Current orchestrator used by the pipeline, giving access to content and other helps.</param>
    /// <param name="log">Application logger</param>
    public TextExtractionHandler(
        string stepName,
        IPipelineOrchestrator orchestrator,
        ILogger<TextExtractionHandler>? log = null)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
        this._log = log ?? NullLogger<TextExtractionHandler>.Instance;

        this._log.LogInformation("Handler ready: {0}", stepName);
    }

    /// <inheritdoc />
    public string StepName { get; }

    /// <inheritdoc />
    public async Task<(bool success, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken)
    {
        foreach (DataPipeline.FileDetails file in pipeline.Files)
        {
            var sourceFile = file.Name;
            var destFile = $"{file.Name}.extract.txt";
            BinaryData fileContent = await this._orchestrator.ReadFileAsync(pipeline, sourceFile, cancellationToken).ConfigureAwait(false);
            string text = string.Empty;
            string extractType = MimeTypes.PlainText;

            switch (file.Type)
            {
                case MimeTypes.PlainText:
                    this._log.LogDebug("Extracting text from plain text file {0}", file.Name);
                    text = fileContent.ToString();
                    break;

                case MimeTypes.MarkDown:
                    this._log.LogDebug("Extracting text from MarkDown file {0}", file.Name);
                    text = fileContent.ToString();
                    extractType = MimeTypes.MarkDown;
                    break;

                case MimeTypes.MsWord:
                    this._log.LogDebug("Extracting text from MS Word file {0}", file.Name);
                    if (fileContent.ToArray().Length > 0)
                    {
                        text = new MsWordDecoder().DocToText(fileContent);
                    }

                    break;

                case MimeTypes.Pdf:
                    this._log.LogDebug("Extracting text from PDF file {0}", file.Name);
                    if (fileContent.ToArray().Length > 0)
                    {
                        text = new PdfDecoder().DocToText(fileContent);
                    }

                    break;

                default:
                    throw new NotSupportedException($"File type not supported: {file.Type}");
            }

            this._log.LogDebug("Saving extracted text file {0}", destFile);
            await this._orchestrator.WriteTextFileAsync(pipeline, destFile, text, cancellationToken).ConfigureAwait(false);

            file.GeneratedFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
            {
                Name = destFile,
                Size = text.Length,
                Type = extractType,
                IsPartition = false
            });
        }

        return (true, pipeline);
    }
}