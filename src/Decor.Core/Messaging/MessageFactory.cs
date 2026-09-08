namespace Decor.Core.Messaging;

public static class MessageFactory
{
    public static MessageData Error(ErrorMessage message, string? customMessage = null)
        => new(MessageResult.Error, customMessage ?? FormatErrorMessage(message));

    public static MessageData OK(SuccessMessage message, string? customMessage = null)
        => new(MessageResult.Success, customMessage ?? FormatSuccessMessage(message));

    public static MessageData Info(InfoMessage message, string? customMessage = null)
        => new(MessageResult.Info, customMessage ?? FormatInfoMessage(message));

    public static MessageData Alert(AlertMessage message, string? customMessage = null)
        => new(MessageResult.Alert, customMessage ?? FormatAlertMessage(message));

    public static MessageData Question(QuestionMessage message)
        => new(MessageResult.None, FormatQuestionMessage(message));


    private static string FormatErrorMessage(ErrorMessage message) => message switch
    {
        ErrorMessage.FailedDelete => "Não foi possível excluir o registro.",
        ErrorMessage.FailedSave => "Erro ao salvar os dados.",
        ErrorMessage.ConnectionLost => "Falha na conexão com o servidor.",
        ErrorMessage.UnexpectedError => "Ocorreu um erro inesperado.",
        _ => "Erro desconhecido."
    };

    private static string FormatSuccessMessage(SuccessMessage message) => message switch
    {
        SuccessMessage.Added => "Registro adicionado com sucesso!",
        SuccessMessage.Updated => "Dados atualizados corretamente!",
        SuccessMessage.SuccessfulOperation => "Operação concluída com êxito!",
        _ => "Ação bem-sucedida!"
    };

    private static string FormatInfoMessage(InfoMessage message) => message switch
    {
        InfoMessage.NoRecordsFound => "Nenhum registro foi encontrado.",
        InfoMessage.LoadingData => "Carregando dados...",
        InfoMessage.OperationInProgress => "Operação em andamento.",
        _ => "Informação não especificada."
    };

    private static string FormatAlertMessage(AlertMessage message) => message switch
    {
        AlertMessage.UserNeedsConfirmation => "Confirme antes de continuar.",
        AlertMessage.HighRiskAction => "Essa ação pode ter consequências graves!",
        AlertMessage.RequiresAdminApproval => "Esta operação exige autorização de um administrador.",
        _ => "Alerta!"
    };

    private static string FormatQuestionMessage(QuestionMessage message) => message switch
    {
        QuestionMessage.ConfirmDelete => "Tem certeza que deseja deletar esse registro?",
        QuestionMessage.ConfirmSave => "Deseja salvar as alterações?",
        QuestionMessage.ConfirmExit => "Tem certeza que deseja sair?",
        QuestionMessage.ConfirmAction => "Deseja continuar com esta ação?",
        QuestionMessage.CloseAndApplyTheme => "Para aplicar o novo tema, todas as janelas de trabalho abertas serão fechadas.\n\nDeseja continuar?",
        _ => "Confirmação necessária."
    };

}
