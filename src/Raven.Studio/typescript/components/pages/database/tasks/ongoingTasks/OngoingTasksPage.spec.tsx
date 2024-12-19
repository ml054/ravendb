import { rtlRender } from "test/rtlTestUtils";
import React from "react";

import * as stories from "./OngoingTasksPage.stories";
import { composeStories, composeStory } from "@storybook/react";
import { boundCopy } from "components/utils/common";
import { within } from "@testing-library/dom";

const { EmptyView, FullView } = composeStories(stories);

const selectors = {
    emptyScriptText: /Following scripts don't match any documents/i,
    deleteTaskTitle: /Delete task/,
    editTaskTitle: /Edit task/,
} as const;

describe("OngoingTasksPage", function () {
    it("can render empty view", async () => {
        const { screen } = rtlRender(<EmptyView />);

        expect(await screen.findByText(/No tasks have been created for this Database Group/)).toBeInTheDocument();
    });

    it("can render full view", async () => {
        const { screen } = rtlRender(<FullView />);

        expect(await screen.findByText(/RavenDB ETL/)).toBeInTheDocument();
    });

    describe("RavenETL", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.RavenEtlTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("raven-etls"));

            expect(await container.findByText(/RavenDB ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Topology Discovery URLs/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.RavenEtlTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("raven-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.RavenEtlTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("raven-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.RavenEtlTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("raven-etls"));

            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("SQL", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.SqlTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("sql-etls"));
            expect(await container.findByText(/SQL ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            const target = await container.findByTitle("Destination <database>@<server>");
            expect(target).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.SqlTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("sql-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.SqlTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("sql-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.SqlTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("sql-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("OLAP", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.OlapTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("olap-etls"));
            expect(await container.findByText(/OLAP ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Destination/)).toBeInTheDocument();

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.OlapTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("olap-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.OlapTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("olap-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.OlapTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("olap-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("Kafka ETL", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.KafkaEtlTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-etls"));
            expect(await container.findByText(/KAFKA ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.KafkaEtlTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.KafkaEtlTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.KafkaEtlTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("RabbitMQ ETL", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.RabbitEtlTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-etls"));
            expect(await container.findByText(/RabbitMQ ETL/i)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.RabbitEtlTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.RabbitEtlTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.RabbitEtlTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("Azure Queue Storage ETL", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.AzureQueueStorageEtlTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("azure-queue-storage-etls"));

            expect(await container.findByText(/AZURE QUEUE STORAGE ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.AzureQueueStorageEtlTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("azure-queue-storage-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.AzureQueueStorageEtlTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("azure-queue-storage-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.AzureQueueStorageEtlTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("azure-queue-storage-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("Kafka Sink", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.KafkaSinkTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-sinks"));

            expect(await container.findByText(/KAFKA SINK/)).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();
        });

        it("can render disabled", async () => {
            const View = boundCopy(stories.KafkaSinkTemplate, {
                disabled: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("kafka-sinks"));

            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);
        });
    });

    describe("RabbitMq Sink", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.RabbitSinkTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-sinks"));
            expect(await container.findByText(/RABBITMQ SINK/)).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();
        });

        it("can render disabled", async () => {
            const View = boundCopy(stories.RabbitSinkTemplate, {
                disabled: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("rabbitmq-sinks"));

            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);
        });
    });

    describe("ElasticSearch", function () {
        it("can render disabled and not completed", async () => {
            const View = boundCopy(stories.ElasticSearchTemplate, {
                disabled: true,
                completed: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("elastic-search-etls"));
            expect(await container.findByText(/Elasticsearch ETL/)).toBeInTheDocument();
            expect(await container.findByText(/Disabled/)).toBeInTheDocument();
            expect(container.queryByText(/Enabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText("http://elastic1:8081")).toBeInTheDocument();

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();

            //wait for progress
            await container.findAllByText(/Disabled/i);
        });

        it("can render completed", async () => {
            const View = boundCopy(stories.ElasticSearchTemplate, {
                completed: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("elastic-search-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);
        });

        it("can render enabled and not completed", async () => {
            const View = boundCopy(stories.ElasticSearchTemplate, {
                completed: false,
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("elastic-search-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText("Running");
        });

        it("can notify about empty script", async () => {
            const View = boundCopy(stories.ElasticSearchTemplate, {
                completed: true,
                emptyScript: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("elastic-search-etls"));
            const detailsBtn = await container.findByTitle(/Click for details/);
            await fireClick(detailsBtn);

            //wait for progress
            await container.findAllByText(/Up to date/i);

            expect(await container.findByText(selectors.emptyScriptText)).toBeInTheDocument();
        });
    });

    describe("Replication Sink", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.ReplicationSinkTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("replication-sinks"));
            expect(await container.findByRole("heading", { name: /Replication Sink/ })).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Hub Database/)).toBeInTheDocument();
            expect(await container.findByText(/Connection String/)).toBeInTheDocument();
            expect(await container.findByText(/Actual Hub URL/)).toBeInTheDocument();
            expect(await container.findByText(/Hub Name/)).toBeInTheDocument();
        });
    });

    describe("Replication Hub", function () {
        it("can render hub w/o connections", async () => {
            const View = boundCopy(stories.ReplicationHubTemplate, {
                disabled: false,
                withOutConnections: true,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("replication-hubs"));
            expect(await container.findByRole("heading", { name: /Replication Hub/ })).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/No sinks connected/)).toBeInTheDocument();
        });

        it("can render hub w/ connections", async () => {
            const View = boundCopy(stories.ReplicationHubTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("replication-hubs"));
            expect(await container.findByRole("heading", { name: /Replication Hub/ })).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Task Name/)).toBeInTheDocument();
            expect(await container.findByText(/Sink Database/)).toBeInTheDocument();
            expect(await container.findByText(/target-hub-db/)).toBeInTheDocument();
            expect(await container.findByText(/Actual Sink URL/)).toBeInTheDocument();

            expect(await container.findByText(/Last DB Etag/)).toBeInTheDocument();
            expect(await container.findByText(/Last Sent Etag/)).toBeInTheDocument();
        });
    });

    describe("Periodic Backup", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.PeriodicBackupTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("backups"));
            expect(await container.findByText(/Periodic Backup/)).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Destinations/)).toBeInTheDocument();
            expect(await container.findByText(/Last Full Backup/)).toBeInTheDocument();
            expect(await container.findByText(/Last Incremental Backup/)).toBeInTheDocument();
            expect(await container.findByText(/Next Estimated Backup/)).toBeInTheDocument();
            expect(await container.findByText(/Retention Policy/)).toBeInTheDocument();
        });
    });

    describe("External Replication", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.ExternalReplicationTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("external-replications"));
            expect(await container.findByRole("heading", { name: /External Replication/ })).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Connection String/)).toBeInTheDocument();
            expect(await container.findByText(/Destination Database/)).toBeInTheDocument();
            expect(await container.findByText(/Actual Destination URL/)).toBeInTheDocument();
            expect(await container.findByText(/Topology Discovery URLs/)).toBeInTheDocument();

            // edit, delete button should be present for non-server wide
            expect(container.queryByTitle(selectors.deleteTaskTitle)).toBeInTheDocument();
            expect(container.queryByTitle(selectors.editTaskTitle)).toBeInTheDocument();

            expect(await container.findByText(/Last DB Etag/)).toBeInTheDocument();
            expect(await container.findByText(/Last Sent Etag/)).toBeInTheDocument();
        });

        it("can render server wide", async () => {
            const Story = composeStory(stories.ExternalReplicationServerWide, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("external-replications"));
            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            // edit, delete button not present for server wide
            expect(container.queryByTitle(selectors.deleteTaskTitle)).not.toBeInTheDocument();
            expect(container.queryByTitle(selectors.editTaskTitle)).not.toBeInTheDocument();
        });
    });

    describe("Subscription", function () {
        it("can render enabled", async () => {
            const View = boundCopy(stories.SubscriptionTemplate, {
                disabled: false,
            });

            const Story = composeStory(View, stories.default);

            const { screen, fireClick } = rtlRender(<Story />);
            const container = within(await screen.findByTestId("subscriptions"));
            expect(await container.findByRole("heading", { name: /Subscription/ })).toBeInTheDocument();
            expect(await container.findByText(/Enabled/)).toBeInTheDocument();
            expect(container.queryByText(/Disabled/)).not.toBeInTheDocument();

            const detailsBtn = await container.findByTitle(/Click for details/);

            await fireClick(detailsBtn);

            expect(await container.findByText(/Last Batch Ack Time/)).toBeInTheDocument();
            expect(await container.findByText(/Last Client Connection Time/)).toBeInTheDocument();
            expect(await container.findByText(/Change vector for next batch/)).toBeInTheDocument();
        });
    });
});
