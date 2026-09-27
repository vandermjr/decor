namespace Decor.AvaloniaUI.Icons;

public readonly record struct DecorIconId(string Value)
{
    public static class Application
    {
        public static DecorIconId Home { get; } = new("Application.Home");
        public static DecorIconId Settings { get; } = new("Application.Settings");
        public static DecorIconId Help { get; } = new("Application.Help");
    }

    public static class Modules
    {
        public static DecorIconId Cadastros { get; } = new("Modules.Cadastros");
        public static DecorIconId Compras { get; } = new("Modules.Compras");
        public static DecorIconId Estoque { get; } = new("Modules.Estoque");
        public static DecorIconId Comercial { get; } = new("Modules.Comercial");
        public static DecorIconId Servicos { get; } = new("Modules.Servicos");
        public static DecorIconId Financeiro { get; } = new("Modules.Financeiro");
        public static DecorIconId Configuracoes { get; } = new("Modules.Configuracoes");
    }

    public static class Forms
    {
        public static DecorIconId Products { get; } = new("Forms.Products");
        public static DecorIconId Brands { get; } = new("Forms.Brands");
        public static DecorIconId Classifications { get; } = new("Forms.Classifications");
        public static DecorIconId TermDelivery { get; } = new("Forms.TermDelivery");
        public static DecorIconId Users { get; } = new("Forms.Users");
        public static DecorIconId PermissionGroups { get; } = new("Forms.PermissionGroups");
        public static DecorIconId DatabaseMaintenance { get; } = new("Forms.DatabaseMaintenance");
    }

    public static class Actions
    {
        public static DecorIconId View { get; } = new("Actions.View");
        public static DecorIconId Create { get; } = new("Actions.Create");
        public static DecorIconId Edit { get; } = new("Actions.Edit");
        public static DecorIconId Delete { get; } = new("Actions.Delete");
        public static DecorIconId Generic { get; } = new("Actions.Generic");
        public static DecorIconId Copy { get; } = new("Actions.Copy");
    }

    public static class User
    {
        public static DecorIconId Profile { get; } = new("User.Profile");
        public static DecorIconId Preferences { get; } = new("User.Preferences");
        public static DecorIconId ChangePassword { get; } = new("User.ChangePassword");
        public static DecorIconId Notifications { get; } = new("User.Notifications");
        public static DecorIconId SignOut { get; } = new("User.SignOut");
    }

    public static class Common
    {
        public static DecorIconId Calendar { get; } = new("Common.Calendar");
        public static DecorIconId Clock { get; } = new("Common.Clock");
    }
}