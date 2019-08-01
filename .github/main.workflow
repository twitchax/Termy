workflow "build and deploy" {
  on = "push"
  resolves = ["GitHub Action for Docker"]
}

action "build" {
  uses = "actions/docker/cli@86ff551d26008267bb89ac11198ba7f1d807b699"
  args = "build -t twitchax/termy ."
}

action "login" {
  uses = "actions/docker/login@86ff551d26008267bb89ac11198ba7f1d807b699"
  needs = ["build"]
  secrets = [
    "DOCKER_USERNAME",
    "DOCKER_PASSWORD",
  ]
}

action "GitHub Action for Docker" {
  uses = "actions/docker/cli@86ff551d26008267bb89ac11198ba7f1d807b699"
  needs = ["login"]
  args = "push twitchax/termy"
}
