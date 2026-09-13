pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '20'))
        disableConcurrentBuilds()
        timestamps()
        skipDefaultCheckout(true)
    }

    triggers {
        githubPush()
        pollSCM('H/2 * * * *')
    }

    environment {
        APP_NAME = 'yapasakay'
        DEPLOY_HOST = 'yapasakay.com'
        PRODUCTION_RELEASE_FILE = '/var/lib/pricebadz/release.json'
        SSH_CREDENTIALS_ID = 'yapasakay-prod-ssh'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.GIT_BRANCH = checkoutInfo.GIT_BRANCH
                    env.GIT_COMMIT = checkoutInfo.GIT_COMMIT
                }
                sh 'git rev-parse --short HEAD > .git-short-sha'
            }
        }

        stage('Build') {
            steps {
                sh '''#!/usr/bin/env bash
                    set -euxo pipefail

                    dotnet --info
                    node --version
                    npm --version

                    rm -rf .jenkins
                    mkdir -p .jenkins/publish .jenkins/package

                    previous_commit="${GIT_PREVIOUS_SUCCESSFUL_COMMIT:-}"
                    if [ -z "${previous_commit}" ] || ! git cat-file -e "${previous_commit}^{commit}" 2>/dev/null; then
                        if git rev-parse --verify HEAD~1 >/dev/null 2>&1; then
                            previous_commit="$(git rev-parse HEAD~1)"
                        fi
                    fi

                    npm_ci_for() {
                        local app_dir="$1"
                        local app_name="$2"

                        if [ -n "${previous_commit}" ] && git diff --quiet "${previous_commit}" HEAD -- "${app_dir}/package.json" "${app_dir}/package-lock.json"; then
                            echo "No package manifest changes for ${app_name}; using npm cache first."
                            npm ci --prefer-offline --no-audit --prefix "${app_dir}"
                        else
                            echo "Package manifest changed for ${app_name}; allowing npm registry lookup."
                            npm ci --no-audit --prefix "${app_dir}"
                        fi
                    }

                    npm_ci_for web/customer customer
                    npm_ci_for web/admin admin

                    bash deploy/sync-wwwroot.sh

                    dotnet publish backend/YaPasakay.Api/YaPasakay.Api.csproj \
                        -c Release \
                        -r linux-x64 \
                        --self-contained true \
                        -p:UseAppHost=true \
                        -o .jenkins/publish/yapasakay

                    test -x .jenkins/publish/yapasakay/YaPasakay.Api
                '''
            }
        }

        stage('Package') {
            steps {
                sh '''#!/usr/bin/env bash
                    set -euxo pipefail

                    commit_short="$(cat .git-short-sha)"
                    package="${APP_NAME}-${BUILD_NUMBER}-${commit_short}.tar.gz"

                    tar -C .jenkins/publish/yapasakay -czf ".jenkins/package/${package}" .
                    printf '%s' "${package}" > .jenkins/package/name
                '''
                archiveArtifacts artifacts: '.jenkins/package/*.tar.gz', fingerprint: true
            }
        }

        stage('Deploy Branch Sites') {
            when {
                expression {
                    !env.CHANGE_ID && env.GIT_BRANCH in ['main', 'origin/main', 'Staging', 'origin/Staging']
                }
            }
            steps {
                withCredentials([sshUserPrivateKey(
                    credentialsId: env.SSH_CREDENTIALS_ID,
                    keyFileVariable: 'SSH_KEY',
                    usernameVariable: 'SSH_USER'
                )]) {
                    sh '''#!/usr/bin/env bash
                        set -euxo pipefail

                        bash deploy/branch-targets.sh "${GIT_BRANCH}" > .jenkins/targets
                        package="$(cat .jenkins/package/name)"
                        ssh_opts="-i ${SSH_KEY} -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new"

                        while IFS='|' read -r target_name target_host deploy_path deploy_service env_file health_url release_root; do
                            if [ -z "${target_name}" ] || [[ "${target_name:0:1}" == "#" ]]; then
                                continue
                            fi

                            echo "Deploying ${target_name} to ${target_host}:${deploy_path}"
                            remote_package="/tmp/${target_name}-${package}"
                            remote_script="/tmp/${target_name}-deploy-${BUILD_NUMBER}-${GIT_COMMIT}.sh"
                            version_source_file=""
                            if [ "${target_name}" = "pasakyaman" ] || [ "${target_name}" = "trygoride" ]; then
                                version_source_file="${PRODUCTION_RELEASE_FILE}"
                            fi

                            scp ${ssh_opts} ".jenkins/package/${package}" "${SSH_USER}@${target_host}:${remote_package}"
                            scp ${ssh_opts} deploy/jenkins-deploy.sh "${SSH_USER}@${target_host}:${remote_script}"

                            ssh -n ${ssh_opts} "${SSH_USER}@${target_host}" \
                                "bash '${remote_script}' '${remote_package}' '${deploy_path}' '${deploy_service}' '${BUILD_NUMBER}' '${GIT_COMMIT:-unknown}' '${target_name}' '${env_file}' '${health_url}' '${release_root}' '' '${version_source_file}'"
                        done < .jenkins/targets
                    '''
                }
            }
        }
    }

    post {
        always {
            sh 'rm -rf .jenkins'
        }
    }
}
