import { useEffect, useState } from "react";
import { App, Button, DatePicker, Form, Input, Modal, Select, Space, Table, Tag, Typography } from "antd";
import type { Dayjs } from "dayjs";
import { PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { apiErrorMessage } from "@/shared/api/api-error";
import {
  createContentArticleSeries,
  fetchContentArticleSeries,
  fetchContentBrands,
  fetchContentPackages,
  type ContentArticleSeries,
  type ContentBrand,
  type ContentPackage,
} from "@/shared/api/content.api";
import {
  ContentLineageTrail,
  seriesBrandAngle,
  seriesProgressLabel,
  seriesTerritory,
} from "@/modules/content/content-lineage";

const STATUSES = ["DRAFT", "PLANNED", "ACTIVE", "PAUSED", "COMPLETED", "CANCELLED"];

export function ContentArticleSeriesPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const prefillPackageId = searchParams.get("packageId") ?? undefined;
  const [rows, setRows] = useState<ContentArticleSeries[]>([]);
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [cores, setCores] = useState<ContentPackage[]>([]);
  const [loading, setLoading] = useState(false);
  const [brandId, setBrandId] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [corePackageId, setCorePackageId] = useState<string>();
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [open, setOpen] = useState(false);
  const [form] = Form.useForm();

  const load = async () => {
    setLoading(true);
    try {
      const [list, brandRows, coreRows] = await Promise.all([
        fetchContentArticleSeries({
          brandId,
          status,
          corePackageId,
          from: range?.[0]?.startOf("day").toISOString(),
          to: range?.[1]?.endOf("day").toISOString(),
        }),
        fetchContentBrands(),
        fetchContentPackages({ coresOnly: true }),
      ]);
      setRows(list);
      setBrands(brandRows);
      setCores(coreRows);
    } catch (e) {
      message.error(apiErrorMessage(e, "Lỗi"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [brandId, status, corePackageId, range]);

  useEffect(() => {
    if (!prefillPackageId) return;
    void (async () => {
      const packs = await fetchContentPackages({ coresOnly: false });
      setPackages(packs);
      form.setFieldsValue({ sourcePackageId: prefillPackageId, episodeCount: 10 });
      setOpen(true);
    })();
  }, [prefillPackageId, form]);

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <Space wrap style={{ justifyContent: "space-between", width: "100%" }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Series
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            Series là câu chuyện dài hạn. Episode là từng bước. Bài viết là output — không phải 100 bài rời.
          </Typography.Paragraph>
          <ContentLineageTrail
            items={[
              { label: "Idea Pool", to: "/content/pool" },
              { label: "Góc brand", to: "/content/packages" },
              { label: "Series" },
            ]}
          />
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={async () => {
              const packs = await fetchContentPackages({ coresOnly: false });
              setPackages(packs);
              setOpen(true);
            }}
          >
            Tạo từ góc brand
          </Button>
        </Space>
      </Space>
      <Space wrap>
        <Select
          allowClear
          placeholder="Brand"
          style={{ width: 180 }}
          value={brandId}
          onChange={setBrandId}
          options={brands.map((b) => ({ value: b.id, label: b.name }))}
        />
        <Select
          allowClear
          placeholder="Status"
          style={{ width: 160 }}
          value={status}
          onChange={setStatus}
          options={STATUSES.map((s) => ({ value: s, label: s }))}
        />
        <Select
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder="Core Idea"
          style={{ width: 280 }}
          value={corePackageId}
          onChange={setCorePackageId}
          options={cores.map((p) => ({ value: p.id, label: p.title }))}
        />
        <DatePicker.RangePicker value={range} onChange={setRange} />
      </Space>
      <Table
        rowKey="id"
        loading={loading}
        dataSource={rows}
        onRow={(row) => ({ onClick: () => navigate(`/content/article-series/${row.id}`) })}
        columns={[
          { title: "Brand", dataIndex: "brandName", width: 110 },
          { title: "Code", dataIndex: "code", width: 140 },
          {
            title: "Series",
            dataIndex: "name",
            render: (v, r) => <Link to={`/content/article-series/${r.id}`}>{v}</Link>,
          },
          { title: "Core Idea", dataIndex: "coreIdeaTitle", ellipsis: true },
          {
            title: "Brand Angle",
            ellipsis: true,
            render: (_, r) => seriesBrandAngle(r) || "Chưa có",
          },
          {
            title: "Territory",
            width: 180,
            ellipsis: true,
            render: (_, r) => seriesTerritory(r) || "Chưa có",
          },
          { title: "Status", dataIndex: "status", width: 110, render: (v) => <Tag>{v}</Tag> },
          {
            title: "Episodes",
            width: 100,
            render: (_, r) => `${r.plannedEpisodeRows}/${r.episodeCount}`,
          },
          {
            title: "Progress",
            width: 180,
            render: (_, r) => seriesProgressLabel(r),
          },
        ]}
      />
      <Modal
        title="Tạo Series từ Brand Adaptation"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={async () => {
          const values = await form.validateFields();
          try {
            const created = await createContentArticleSeries(values);
            setOpen(false);
            form.resetFields();
            navigate(`/content/article-series/${created.id}`);
          } catch (e) {
            message.error(apiErrorMessage(e, "Lỗi"));
          }
        }}
      >
        <Form form={form} layout="vertical" initialValues={{ episodeCount: 10 }}>
          <Form.Item name="sourcePackageId" label="Góc brand" rules={[{ required: true }]}>
            <Select
              showSearch
              optionFilterProp="label"
              options={packages.map((p) => ({
                value: p.id,
                label: `${p.brandName} · ${p.title}`,
              }))}
            />
          </Form.Item>
          <Form.Item name="name" label="Tên Series">
            <Input />
          </Form.Item>
          <Form.Item name="episodeCount" label="Độ dài Blueprint">
            <Select options={[5, 10, 20, 30, 50, 100].map((n) => ({ value: n, label: `${n} episode` }))} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  );
}
