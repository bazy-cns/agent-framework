# Agent Framework audit call graph

## Python checkpoint restore

```text
Workflow.run(... checkpoint_id=...)
  -> Workflow._execute_with_message_or_checkpoint(...)
  -> runner.restore_from_checkpoint(checkpoint_id, checkpoint_storage)
  -> CheckpointStorage.load(checkpoint_id)
  -> FileCheckpointStorage.load(checkpoint_id)
      -> _validate_file_path(checkpoint_id)
      -> json.load(file)
      -> decode_checkpoint_value(encoded_checkpoint, allowed_types=self._allowed_types)
          -> _decode(value)
          -> _base64_to_unpickle(encoded, allowed_types)
             -> _RestrictedUnpickler(...).load() when allowed_types is not None
             -> pickle.loads(...) when allowed_types is None
      -> WorkflowCheckpoint.from_dict(decoded_checkpoint_dict)
```

## Python workflow state import

```text
WorkflowCheckpoint.state/messages/pending_request_info_events
  -> runner restore path
  -> WorkflowExecutor.on_checkpoint_restore(state)
      -> decode_checkpoint_value(...) for execution contexts
  -> executor-specific on_checkpoint_restore(state)
```

## Python MCP tools

```text
MCPTool.load_tools()
  -> session.list_tools(...)
  -> allowed_tools filter / name normalization
  -> FunctionTool(func=_call_tool_with_runtime_kwargs, ...)

Function invocation loop
  -> FunctionTool.invoke(...)
  -> _call_tool_with_runtime_kwargs(**kwargs)
  -> MCPTool.call_tool(remote_tool_name, **kwargs)
      -> _prepare_call_kwargs(tool_name, kwargs)
          -> declared inputSchema.properties allowlist
          -> additional_tool_argument_names construction-time extras
          -> _MCP_FRAMEWORK_DENYLIST stripping
      -> session.call_tool(tool_name, arguments=filtered_kwargs, meta=meta)
```

## Python file access and memory

```text
FileAccessProvider / FileMemoryProvider tools
  -> AgentFileStore method (read_file/write_file/delete_file/search_files)
  -> FileSystemAgentFileStore rooted storage
      -> relative path normalization
      -> root containment check
      -> symlink/reparse-point segment rejection
      -> disk read/write/delete/search
```

## .NET workflow checkpointing

```text
InProcessExecution.ResumeAsync/ResumeStreamingAsync
  -> InProcessRunner.RestoreCheckpointAsync(...)
  -> CheckpointManagerImpl<TStoreObject>.LoadCheckpointAsync(...)
  -> JsonCheckpointStore.GetCheckpointAsync(...)
  -> FileSystemJsonCheckpointStore.GetFileNameForCheckpoint(sessionId, key)
      -> Uri.EscapeDataString(...).Replace(".", "%2E")
  -> JsonSerializer.Deserialize with workflow JsonTypeInfo/converters
  -> StateManager.ImportStateAsync(checkpoint)
```

## .NET declarative HTTP

```text
HttpRequestExecutor.ExecuteAsync(context)
  -> GetMethod()/GetUrl()/GetHeaders()/GetQueryParameters()/GetBody()
  -> new HttpRequestInfo { Method, Url, Headers, QueryParameters, Body, Timeout, ConnectionName }
  -> IHttpRequestHandler.SendAsync(requestInfo)
  -> DefaultHttpRequestHandler.SendAsync(requestInfo)
      -> non-empty URL/method checks
      -> BuildHttpRequestMessage(requestInfo)
      -> ResolveRequestUri(requestInfo)
      -> HttpClient.SendAsync(httpRequest, ...)
```

## .NET declarative MCP

```text
InvokeMcpToolExecutor.ExecuteAsync(context)
  -> GetServerUrl()/GetServerLabel()/GetToolName()/GetRequireApproval()/GetArguments()/GetHeaders()
  -> if requireApproval:
        store ApprovalSnapshot and emit ToolApprovalRequestContent
     else:
        mcpToolHandler.InvokeToolAsync(serverUrl, serverLabel, toolName, arguments, headers, connectionName, ...)
```
