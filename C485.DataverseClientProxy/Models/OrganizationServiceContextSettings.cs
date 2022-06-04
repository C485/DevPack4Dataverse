namespace C485.DataverseClientProxy.Models;

public class OrganizationServiceContextSettings
{
	public static readonly OrganizationServiceContextSettings Default = new();

	public bool ClearChangesEveryTime { get; set; } = true;

	public bool DetachRetrievedRecords { get; set; } = true;
}