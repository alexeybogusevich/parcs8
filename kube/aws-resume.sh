#!/bin/bash
# Resume a deployment paused with aws-pause.sh: bring the node groups back, wait for the nodes,
# then restore every deployment's replica count.
#
# The system node group must span exactly the zones of the EBS volumes (Postgres, Elasticsearch),
# one node per zone, or a pod can wait forever for a node in its volume's zone.
set -euo pipefail

export AWS_PROFILE=${AWS_PROFILE:-parcs-trial}
export AWS_REGION=${AWS_REGION:-eu-north-1}
CLUSTER=${CLUSTER:-parcs-cluster}
CONTEXT=${CONTEXT:-parcs-aws}
# name:min:desired
NODEGROUPS=${NODEGROUPS:-"system-ac:2:2 daemon-flex:1:1"}
k="kubectl --context $CONTEXT"

expected=0
for spec in $NODEGROUPS; do
  IFS=: read -r ng min desired <<<"$spec"
  aws eks update-nodegroup-config --cluster-name "$CLUSTER" --nodegroup-name "$ng" \
    --scaling-config minSize="$min",desiredSize="$desired" --query update.status --output text >/dev/null
  expected=$((expected + desired))
  echo "node group $ng → $desired"
done

echo "waiting for $expected Ready nodes..."
for _ in $(seq 1 60); do
  ready=$($k get nodes --no-headers 2>/dev/null | awk '$2 == "Ready"' | wc -l)
  [ "$ready" -ge "$expected" ] && break
  sleep 10
done
$k get nodes -L pool,topology.kubernetes.io/zone

for d in $($k get deploy -o name); do
  replicas=$($k get "$d" -o jsonpath='{.metadata.annotations.parcs-paused-replicas}')
  $k scale "$d" --replicas="${replicas:-1}" >/dev/null
  echo "started $d (${replicas:-1})"
done
$k wait --for=condition=Available deploy --all --timeout=600s
echo "Resumed."
