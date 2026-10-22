using System.Collections.ObjectModel;
using Avalonia.Media;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.ViewModels;

public enum ClassificationTreeLevel
{
    Class,
    Family,
    Group,
    Subgroup
}

public sealed class ClassificationTreeNode
{
    public ClassificationTreeNode(
        string name,
        ClassificationTreeLevel level,
        ClassDTO classItem,
        FamilyDTO? familyItem = null,
        GroupDTO? groupItem = null,
        SubgroupDTO? subgroupItem = null)
    {
        Name = name;
        Level = level;
        ClassItem = classItem;
        FamilyItem = familyItem;
        GroupItem = groupItem;
        SubgroupItem = subgroupItem;
    }

    public string Name { get; }
    public ClassificationTreeLevel Level { get; }
    public string LevelName => Level switch
    {
        ClassificationTreeLevel.Class => "Classe",
        ClassificationTreeLevel.Family => "Família",
        ClassificationTreeLevel.Group => "Grupo",
        ClassificationTreeLevel.Subgroup => "Subgrupo",
        _ => string.Empty
    };
    public IBrush LevelBrush => Level switch
    {
        ClassificationTreeLevel.Class => Brush.Parse("#2074B8"),
        ClassificationTreeLevel.Family => Brush.Parse("#63844F"),
        ClassificationTreeLevel.Group => Brush.Parse("#A66A17"),
        ClassificationTreeLevel.Subgroup => Brush.Parse("#755B91"),
        _ => Brushes.Gray
    };
    public ClassDTO ClassItem { get; }
    public FamilyDTO? FamilyItem { get; }
    public GroupDTO? GroupItem { get; }
    public SubgroupDTO? SubgroupItem { get; }
    public ObservableCollection<ClassificationTreeNode> Children { get; } = [];

    public ClassificationTreeNode CopyWithChildren(IEnumerable<ClassificationTreeNode> children)
    {
        var copy = new ClassificationTreeNode(Name, Level, ClassItem, FamilyItem, GroupItem, SubgroupItem);
        foreach (var child in children)
            copy.Children.Add(child);
        return copy;
    }
}
