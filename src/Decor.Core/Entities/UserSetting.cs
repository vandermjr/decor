using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Decor.Core.Entities;

[Table("user_settings")]
public class UserSetting
{
    [Key]
    [Column("UserID")]
    public int UserID { get; set; }

    [Key]
    [Column("SettingKey")]
    public string SettingKey { get; set; } = string.Empty;

    [Column("SettingValue")]
    public string SettingValue { get; set; } = string.Empty;

    [Column("ValueType")]
    public string ValueType { get; set; } = "String";
}
