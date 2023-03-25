﻿using Parcs.Shared;

namespace Parcs.HostAPI.Models.Responses
{
    public class CreateJobCommandResponse
    {
        public CreateJobCommandResponse(Job job)
        {
            JobId = job.Id;
        }

        public Guid JobId { get; set; }
    }
}