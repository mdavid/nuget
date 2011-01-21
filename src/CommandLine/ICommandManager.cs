using System.Collections.Generic;
using System.Reflection;

namespace NuGet {
    public interface ICommandManager {
        IDictionary<CommandAttribute, ICommand> GetCommands();
        CommandAttribute GetCommandAttribute(ICommand command);
        ICommand GetCommand(string commandName);
        IDictionary<OptionAttribute, PropertyInfo> GetCommandOptions(ICommand command);
        void RegisterCommand(ICommand command);
    }
}
