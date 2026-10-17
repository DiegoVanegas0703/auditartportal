namespace Auditart.Application.Abstractions;

public interface IAcroFormFiller
{
    byte[] Fill(string templatePdfPath, IReadOnlyDictionary<string, string> values);
}
