namespace SistemaAlertaCapital.Models;

/// <summary>
/// Dados exibidos na página de erro (Views/Shared/Error.cshtml).
/// </summary>
public class ErrorViewModel
{
    /// <summary>Identificador da requisição que falhou, útil para localizar o erro nos logs.</summary>
    public string? RequestId { get; set; }

    /// <summary>Indica se há um identificador para exibir na tela.</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
