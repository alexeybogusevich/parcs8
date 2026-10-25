#!/bin/bash
# Pause the static PARCS deployment on EKS: stop every workload cleanly, then scale all node groups
# to zero. What keeps running (and billing) while paused: the EKS control plane, the load
# balancers (their hostnames are kept), EBS/EFS volumes, ECR images. Resume with aws-resume.sh.
#
# A Kubernetes manifest alone cannot do this: the EC2 nodes, which are most of the cost, belong to
# the EKS node groups, not to the cluster's API.
set -euo pipefail

export AWS_PROFILE=${AWS_PROFILE:-parcs-trial}
export AWS_REGION=${AWS_REGION:-eu-north-1}
CLUSTER=${CLUSTER:-parcs-cluster}
CONTEXT=${CONTEXT:-parcs-aws}
NODEGROUPS=${NODEGROUPS:-"system-ac daemon-flex"}
k="kubectl --context $CONTEXT"

# Remember each deployment's replica count, then stop it (Postgres and Elasticsearch shut down
# cleanly instead of being killed with their node).
for d in $($k get deploy -o name); do
  replicas=$($k get "$d" -o jsonpath='{.spec.replicas}')
  if [ "$replicas" != "0" ]; then
    $k annotate "$d" parcs-paused-replicas="$replicas" --overwrite >/dev/null
  fi
  $k scale "$d" --replicas=0 >/dev/null
  echo "stopped $d (was $replicas)"
done
$k wait --for=delete pod --all --timeout=300s >/dev/null 2>&1 || true

for ng in $NODEGROUPS; do
  aws eks update-nodegroup-config --cluster-name "$CLUSTER" --nodegroup-name "$ng" \
    --scaling-config minSize=0,desiredSize=0 --query update.status --output text >/dev/null
  echo "node group $ng → 0"
done
# The node-group drain evicts pods, and the PodDisruptionBudgets of the add-ons (coredns,
# ebs-csi-controller) never allow evicting their last replica when no node is left to take it.
# Deleting a pod is not an eviction, so remove whatever still runs on draining nodes.
echo "waiting for the nodes to drain..."
for _ in $(seq 1 60); do
  nodes=$($k get nodes --no-headers 2>/dev/null | wc -l)
  [ "$nodes" -eq 0 ] && break
  for node in $($k get nodes --no-headers 2>/dev/null | awk '$2 ~ /SchedulingDisabled/ {print $1}'); do
    $k -n kube-system get pods --field-selector spec.nodeName="$node" -o name 2>/dev/null \
      | grep -E 'coredns|ebs-csi-controller|efs-csi-controller' \
      | xargs -r $k -n kube-system delete --wait=false >/dev/null 2>&1 || true
  done
  sleep 15
done
echo "Paused ($($k get nodes --no-headers 2>/dev/null | wc -l) nodes left). Resume with kube/aws-resume.sh."
