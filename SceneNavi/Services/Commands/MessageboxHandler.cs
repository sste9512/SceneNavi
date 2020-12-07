using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MediatR;

namespace SceneNavi.Services.Commands
{
    public class MessageboxHandler : INotificationHandler<MessageBoxCommand>
    {

       
        public Task Handle(MessageBoxCommand notification, CancellationToken cancellationToken)
        {
            MessageBox.Show("this worked", "Vertex Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }
    }
}