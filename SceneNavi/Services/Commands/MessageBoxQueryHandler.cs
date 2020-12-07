using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace SceneNavi.Services.Commands
{
    public class MessageBoxQueryHandler : IRequestHandler<MessageBoxRequest>
    {
        public Task<Unit> Handle(MessageBoxRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}