namespace Auditart.Application.Abstractions;

public interface IPdfFormReader
{
    /// <summary>Lee campos AcroForm con valor (vacíos y Off se omiten).</summary>
    IReadOnlyDictionary<string, string> ReadFilledFields(Stream pdfStream);
}
