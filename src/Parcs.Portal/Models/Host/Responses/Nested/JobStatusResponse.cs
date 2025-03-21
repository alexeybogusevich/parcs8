﻿using Parcs.Core.Models;

namespace Parcs.Portal.Models.Host.Responses.Nested
{
    public class JobStatusResponse
    {
        public JobStatusResponse()
        {
        }

        public JobStatusResponse(JobStatus status, DateTime createDateUtc)
        {
            Status = status;
            CreateDateUtc = createDateUtc;
        }

        public JobStatus Status { get; set; }

        public DateTime CreateDateUtc { get; set; }

        public override string ToString()
        {
            return $"{Status} at {CreateDateUtc.ToLocalTime()}";
        }
    }
}