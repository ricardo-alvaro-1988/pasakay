pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '20'))
        disableConcurrentBuilds()
        timestamps()
    }

    triggers {
        pollSCM('H/2 * * * *')
    }

    environment {
        APP_NAME = 'yapasakay'
        DEPLOY_HOST = 'yapasakay.com'
        DEPLOY_PATH = '/var/www/yapasakay'
        DEPLOY_SERVICE = 'yapasakay.service'
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

        stage('Deploy Production') {
            when {
                anyOf {
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
                        remote_package="/tmp/${package}"
                        ssh_opts="-i ${SSH_KEY} -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new"

                        scp ${ssh_opts} ".jenkins/package/${package}" "${SSH_USER}@${DEPLOY_HOST}:${remote_package}"
                        scp ${ssh_opts} deploy/jenkins-deploy.sh "${SSH_USER}@${DEPLOY_HOST}:/tmp/yapasakay-jenkins-deploy.sh"

                        ssh ${ssh_opts} "${SSH_USER}@${DEPLOY_HOST}" \
                            "bash /tmp/yapasakay-jenkins-deploy.sh '${remote_package}' '${DEPLOY_PATH}' '${DEPLOY_SERVICE}' '${BUILD_NUMBER}' '${GIT_COMMIT}'"
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
