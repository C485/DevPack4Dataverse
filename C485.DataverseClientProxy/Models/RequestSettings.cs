using System;

namespace C485.DataverseClientProxy.Models;

public class RequestSettings
{
	/// <summary>
	///  Guid of SystemUser record id
	/// </summary>
	public Guid? ImpersonateAsUserByDataverseId { get; set; }

	public bool SkipPluginExecution { get; set; }
}