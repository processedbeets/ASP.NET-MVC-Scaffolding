using EnvDTE;

namespace T4Scaffolding.Core.PrimaryKeyLocators
{
    public interface IPrimaryKeyLocator
    {
        bool IsPrimaryKey(CodeClass codeClass, CodeProperty codeProperty);
    }
}