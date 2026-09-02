pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '20'))
        disableConcurrentBuilds()
        timestamps()
    }

    triggers {
        githubPush()
        pollSCM('H/2 * * * *')
    }

    environment {
        APP_NAME = 'yapasakay'
        DEPLOY_HOST = 'yapasakay.com'
        SSH_CREDENTIALS_ID = 'yapasakay-prod-ssh'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
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

                    npm ci --prefix web/customer
                    npm ci --prefix web/admin

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

        stage('Deploy Production Sites') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    expression { env.BRANCH_NAME == null || env.BRANCH_NAME == '' }
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

                        package="$(cat .jenkins/package/name)"
                        ssh_opts="-i ${SSH_KEY} -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new"

                        while IFS='|' read -r target_name target_host deploy_path deploy_service env_file health_url release_root; do
                            if [ -z "${target_name}" ] || [[ "${target_name}" == \#* ]]; then
                                continue
                            fi

                            echo "Deploying ${target_name} to ${target_host}:${deploy_path}"
                            remote_package="/tmp/${target_name}-${package}"

                            scp ${ssh_opts} ".jenkins/package/${package}" "${SSH_USER}@${target_host}:${remote_package}"
                            scp ${ssh_opts} deploy/jenkins-deploy.sh "${SSH_USER}@${target_host}:/tmp/yapasakay-jenkins-deploy.sh"

                            ssh ${ssh_opts} "${SSH_USER}@${target_host}" \
                                "bash /tmp/yapasakay-jenkins-deploy.sh '${remote_package}' '${deploy_path}' '${deploy_service}' '${BUILD_NUMBER}' '${GIT_COMMIT:-unknown}' '${target_name}' '${env_file}' '${health_url}' '${release_root}'"
                        done <<TARGETS
yapasakay|${DEPLOY_HOST}|/var/www/yapasakay|yapasakay.service|/etc/yapasakay/yapasakay-api.env|http://127.0.0.1:5003/health|/var/www/releases/yapasakay
pricebadz|${DEPLOY_HOST}|/var/www/pricebadz|pricebadz.service|/etc/pricebadz/pricebadz-api.env|http://127.0.0.1:5004/health|/var/www/releases/pricebadz
pasakyaman|${DEPLOY_HOST}|/var/www/pasakyaman|pasakyaman.service|/etc/pasakyaman/pasakyaman-api.env|http://127.0.0.1:5005/health|/var/www/releases/pasakyaman
TARGETS
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
